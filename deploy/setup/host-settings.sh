#!/bin/bash
# Secret-bearing JSON goes directly to dotnet user-secrets stdin. Never log it.
set -euo pipefail
kind=${1:?Executable name required}
root=${DEVFLOW_HOST_SECRETS_DIR:?Host secret-directory path required}
frontend=${DEVFLOW_FRONTEND_ORIGIN:-http://localhost:3000}
observability=${DEVFLOW_OBSERVABILITY_ENABLED:-false}
case "$kind" in
  gateway)
    jq -n --arg port "${DEVFLOW_GATEWAY_PORT:-8080}" \
      --arg identity "http://127.0.0.1:${DEVFLOW_IDENTITY_API_PORT:-8081}/" \
      --arg notification "http://127.0.0.1:${DEVFLOW_NOTIFICATION_API_PORT:-8082}/" \
      --arg frontend "$frontend" --arg observability "$observability" --arg endpoint "http://127.0.0.1:${DEVFLOW_OTLP_PORT:-4317}" \
      '{"urls":("http://localhost:"+$port),"Cors:AllowedOrigins:0":$frontend,
        "ReverseProxy:Clusters:identity:Destinations:identity-api:Address":$identity,
        "ReverseProxy:Clusters:notification:Destinations:notification-api:Address":$notification,
        "Observability:Enabled":$observability,"Observability:Endpoint":$endpoint}'
    ;;
  identity-api|identity-worker|identity-migrationrunner)
    role=${kind#identity-}
    [ "$role" != migrationrunner ] || role=migrator
    jq -n \
      --arg connection "Host=127.0.0.1;Port=${DEVFLOW_IDENTITY_POSTGRES_PORT:?};Database=DevFlowIdentity;Username=identity_${role};Include Error Detail=false" \
      --arg password_file "$root/identity/${role}-password.secret" \
      --arg observability "$observability" --arg endpoint "http://127.0.0.1:${DEVFLOW_OTLP_PORT:-4317}" \
      '{"ConnectionStrings:ApplicationDb":$connection,"Database:PasswordFile":$password_file,"Observability:Enabled":$observability,"Observability:Endpoint":$endpoint}' >/tmp/host-base.json
    if [ "$kind" = identity-api ]; then
      jq \
        --rawfile jwt /source/identity/jwt-key.secret \
        --rawfile integration /source/identity/integration-key.secret \
        --rawfile hmac /source/identity/registration-hmac.secret \
        --rawfile redis /source/identity/redis-password.secret \
        --arg redis_port "${DEVFLOW_REDIS_PORT:?}" \
        --arg issuer "${DEVFLOW_JWT_ISSUER:-DevFlow.Identity}" \
        --arg audience "${DEVFLOW_JWT_AUDIENCE:-DevFlow}" \
        --arg frontend "$frontend" \
        --arg key_id "${DEVFLOW_EMAIL_ACTIVE_KEY_ID:-local-v1}" \
        --arg public_key "$root/identity/notification-keys/${DEVFLOW_EMAIL_ACTIVE_KEY_ID:-local-v1}.public.pem" \
        --arg urls "http://localhost:${DEVFLOW_IDENTITY_API_PORT:?}" \
        '. + {"Jwt:SecretKey":($jwt|gsub("[\\r\\n]";"")),"Jwt:Issuer":$issuer,"Jwt:Audience":$audience,
              "IntegrationApi:ApiKeys:0":($integration|gsub("[\\r\\n]";"")),
              "Identity:Security:RegistrationRateLimit:KeySecretBase64":($hmac|gsub("[\\r\\n]";"")),
              "Identity:Security:RegistrationRateLimit:RedisConnectionString":("127.0.0.1:"+$redis_port+",password="+($redis|rtrimstr("\n"))),
              "Identity:VerificationEmail:KeyId":$key_id,"Identity:VerificationEmail:PublicKeyFile":$public_key,
              "Cors:AllowedOrigins:0":$frontend,"Cors:AllowedMethods:0":"GET","Cors:AllowedMethods:1":"POST",
              "Cors:AllowedHeaders:0":"Content-Type","Cors:AllowedHeaders:1":"Authorization",
              "Serilog:Using:0":"Serilog.Sinks.Console","Serilog:WriteTo:0:Name":"Console","urls":$urls}' /tmp/host-base.json
    elif [ "$kind" = identity-worker ]; then
      jq --arg port "${DEVFLOW_RABBITMQ_AMQP_PORT:?}" --arg vhost "${DEVFLOW_MESSAGING_VIRTUAL_HOST:-/devflow-local}" \
        --arg password_file "$root/platform/identity-broker-password.secret" \
        --arg endpoint "http://127.0.0.1:${DEVFLOW_OTLP_PORT:-4317}" \
        '. + {"Messaging:Host":"127.0.0.1","Messaging:Port":$port,"Messaging:VirtualHost":$vhost,
              "Messaging:Username":"identity","Messaging:PasswordFile":$password_file,
              "Messaging:Development":"true","Messaging:UseTls":"false","Messaging:QuorumSize":"1",
              "Observability:Endpoint":$endpoint,"Observability:ServiceName":"DevFlow.Identity.Worker"}' /tmp/host-base.json
    else
      cat /tmp/host-base.json
    fi
    ;;
  notification-api|notification-worker|notification-migrationrunner)
    role=runtime
    [ "$kind" != notification-migrationrunner ] || role=migrator
    jq -n \
      --arg connection "Host=127.0.0.1;Port=${DEVFLOW_NOTIFICATION_POSTGRES_PORT:?};Database=DevFlowNotification;Username=notification_${role}" \
      --arg password_file "$root/notification/${role}-password.secret" \
      --arg observability "$observability" --arg endpoint "http://127.0.0.1:${DEVFLOW_OTLP_PORT:-4317}" \
      '{"ConnectionStrings:NotificationDb":$connection,"Database:PasswordFile":$password_file,"Observability:Enabled":$observability,"Observability:Endpoint":$endpoint}' >/tmp/host-base.json
    if [ "$kind" = notification-migrationrunner ]; then
      cat /tmp/host-base.json
    else
      jq \
        --arg mode "${DEVFLOW_EMAIL_MODE:-Mailpit}" --arg sender "${DEVFLOW_EMAIL_SENDER:-DevFlow <auth@example.test>}" --arg verified "${DEVFLOW_EMAIL_DOMAIN_VERIFIED:-false}" \
        --arg frontend "$frontend" --arg key_directory "$root/notification/keys" --arg key_id "${DEVFLOW_EMAIL_ACTIVE_KEY_ID:-local-v1}" \
        --arg port "${DEVFLOW_RABBITMQ_AMQP_PORT:?}" --arg vhost "${DEVFLOW_MESSAGING_VIRTUAL_HOST:-/devflow-local}" \
        --arg smtp_port "${DEVFLOW_MAILPIT_SMTP_PORT:?}" --arg urls "http://localhost:${DEVFLOW_NOTIFICATION_API_PORT:?}" \
        --arg daily_limit "${DEVFLOW_EMAIL_DAILY_LIMIT:-100}" --arg monthly_limit "${DEVFLOW_EMAIL_MONTHLY_LIMIT:-3000}" \
        --arg password_file "$root/platform/notification-broker-password.secret" \
        --arg endpoint "http://127.0.0.1:${DEVFLOW_OTLP_PORT:-4317}" \
        '. + {"Notification:EmailDelivery:Mode":$mode,"Notification:EmailDelivery:Development":"true",
              "Notification:EmailDelivery:Sender":$sender,"Notification:EmailDelivery:FrontendOrigin":$frontend,
              "Notification:EmailDelivery:KeyDirectory":$key_directory,"Notification:EmailDelivery:ActiveKeyId":$key_id,
              "Notification:EmailDelivery:RecipientHashKeyFile":($key_directory+"/recipient-hmac.secret"),
              "Notification:EmailDelivery:ApiKeyFile":($key_directory+"/resend-api.secret"),
              "Notification:EmailDelivery:WebhookSecretFile":($key_directory+"/resend-webhook.secret"),
              "Notification:EmailDelivery:DomainVerified":$verified,
              "Notification:EmailDelivery:MailpitHost":"127.0.0.1","Notification:EmailDelivery:MailpitPort":$smtp_port,
              "Notification:EmailDelivery:DailyLimit":$daily_limit,"Notification:EmailDelivery:MonthlyLimit":$monthly_limit,
              "Messaging:Host":"127.0.0.1","Messaging:Port":$port,"Messaging:VirtualHost":$vhost,
              "Messaging:Username":"notification","Messaging:PasswordFile":$password_file,
              "Messaging:Development":"true","Messaging:UseTls":"false","Messaging:QuorumSize":"1",
              "Observability:Endpoint":$endpoint,"urls":$urls}' /tmp/host-base.json
    fi
    ;;
  *) echo 'Unknown executable for IDE configuration.' >&2; exit 2 ;;
esac

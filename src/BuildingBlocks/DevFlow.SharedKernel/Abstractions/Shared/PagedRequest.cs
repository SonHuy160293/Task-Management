namespace DevFlow.SharedKernel.Abstractions.Shared;

public abstract record PagedRequest
{
    protected PagedRequest() { }

    protected PagedRequest(
        int pageNumber = 1,
        int pageSize = 20,
        string? search = null,
        string? sortBy = null,
        string? sortDirection = null)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        Search = search;
        SortBy = sortBy;
        SortDirection = sortDirection;
    }

    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }

    public int GetSkip()
        => (GetNormalizedPageNumber() - 1) * GetNormalizedPageSize();

    public int GetNormalizedPageNumber()
        => PageNumber <= 0 ? 1 : PageNumber;

    public int GetNormalizedPageSize()
        => PageSize <= 0 ? 20 : Math.Min(PageSize, 100);

    public bool IsSortDescending()
        => string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
}

namespace ByteBill_BS.ViewModels.Common;

public class PaginationViewModel
{
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public int PageSize { get; set; } = 10;
    public string? BaseUrl { get; set; }
    public string? SearchTerm { get; set; }

    /// <summary>Extra query-string parameters to preserve across pages (e.g. filters).</summary>
    public Dictionary<string, string> ExtraParams { get; set; } = new();
    
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
    
    public int StartItem => TotalItems == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
    public int EndItem => Math.Min(CurrentPage * PageSize, TotalItems);
}

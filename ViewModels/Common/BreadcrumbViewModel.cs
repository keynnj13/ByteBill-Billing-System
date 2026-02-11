namespace ByteBill_BS.ViewModels.Common;

public class BreadcrumbViewModel
{
    public List<BreadcrumbItem> Items { get; set; } = new();
    
    public class BreadcrumbItem
    {
        public string Title { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Url { get; set; }
        public bool IsActive { get; set; }
    }
}

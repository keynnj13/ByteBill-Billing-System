using ByteBill_BS.Models.Enums;

namespace ByteBill_BS.Services;

public interface INavigationService
{
    IEnumerable<NavigationItem> GetNavigationItems(UserRole role);
    IEnumerable<NavigationSection> GetNavigationSections(UserRole role);
}

public class NavigationItem
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Badge { get; set; }
    public bool IsActive { get; set; }
}

public class NavigationSection
{
    public string Title { get; set; } = string.Empty;
    public IEnumerable<NavigationItem> Items { get; set; } = Enumerable.Empty<NavigationItem>();
}

public class NavigationService : INavigationService
{
    public IEnumerable<NavigationItem> GetNavigationItems(UserRole role)
    {
        return GetNavigationSections(role).SelectMany(s => s.Items);
    }

    public IEnumerable<NavigationSection> GetNavigationSections(UserRole role)
    {
        return role switch
        {
            UserRole.SuperAdmin => GetSuperAdminNavigation(),
            UserRole.Admin => GetAdminNavigation(),
            UserRole.Billing => GetBillingNavigation(),
            UserRole.Technician => GetTechnicianNavigation(),
            UserRole.Auditor => GetAuditorNavigation(),
            _ => Enumerable.Empty<NavigationSection>()
        };
    }

    private IEnumerable<NavigationSection> GetSuperAdminNavigation()
    {
        return new[]
        {
            new NavigationSection
            {
                Title = "Overview",
                Items = new[]
                {
                    new NavigationItem { Title = "Dashboard", Icon = "home", Url = "/SuperAdmin/Dashboard" }
                }
            },
            new NavigationSection
            {
                Title = "Management",
                Items = new[]
                {
                    new NavigationItem { Title = "Shops", Icon = "store", Url = "/SuperAdmin/Shops" },
                    new NavigationItem { Title = "Users", Icon = "users", Url = "/SuperAdmin/Users" }
                }
            },
            new NavigationSection
            {
                Title = "System",
                Items = new[]
                {
                    new NavigationItem { Title = "System Logs", Icon = "file-text", Url = "/SuperAdmin/SystemLogs" },
                    new NavigationItem { Title = "Settings", Icon = "settings", Url = "/SuperAdmin/Settings" }
                }
            }
        };
    }

    private IEnumerable<NavigationSection> GetAdminNavigation()
    {
        return new[]
        {
            new NavigationSection
            {
                Title = "Overview",
                Items = new[]
                {
                    new NavigationItem { Title = "Dashboard", Icon = "home", Url = "/Admin/Dashboard" }
                }
            },
            new NavigationSection
            {
                Title = "Operations",
                Items = new[]
                {
                    new NavigationItem { Title = "Customers", Icon = "users", Url = "/Admin/Customers" },
                    new NavigationItem { Title = "Job Orders", Icon = "clipboard-list", Url = "/Admin/JobOrders" },
                    new NavigationItem { Title = "Invoices", Icon = "file-invoice", Url = "/Admin/Invoices" },
                    new NavigationItem { Title = "Payments", Icon = "credit-card", Url = "/Admin/Payments" },
                    new NavigationItem { Title = "Adjustments", Icon = "sliders", Url = "/Admin/Adjustments" },
                    new NavigationItem { Title = "Archive", Icon = "archive", Url = "/Archive" }
                }
            },
            new NavigationSection
            {
                Title = "Catalog",
                Items = new[]
                {
                    new NavigationItem { Title = "Services", Icon = "wrench", Url = "/Admin/Services" },
                    new NavigationItem { Title = "Inventory", Icon = "package", Url = "/Admin/Inventory" }
                }
            },
            new NavigationSection
            {
                Title = "Team",
                Items = new[]
                {
                    new NavigationItem { Title = "Users & Roles", Icon = "user-cog", Url = "/Admin/Users" }
                }
            },
            new NavigationSection
            {
                Title = "Insights",
                Items = new[]
                {
                    new NavigationItem { Title = "Reports", Icon = "bar-chart-2", Url = "/Admin/Reports" }
                }
            },
            new NavigationSection
            {
                Title = "System",
                Items = new[]
                {
                    new NavigationItem { Title = "Audit Logs", Icon = "shield", Url = "/Admin/AuditLogs" },
                    new NavigationItem { Title = "Integrations", Icon = "plug", Url = "/Admin/Integrations" }
                }
            }
        };
    }

    private IEnumerable<NavigationSection> GetBillingNavigation()
    {
        return new[]
        {
            new NavigationSection
            {
                Title = "Overview",
                Items = new[]
                {
                    new NavigationItem { Title = "Dashboard", Icon = "home", Url = "/Billing/Dashboard" }
                }
            },
            new NavigationSection
            {
                Title = "Operations",
                Items = new[]
                {
                    new NavigationItem { Title = "Customers", Icon = "users", Url = "/Billing/Customers" },
                    new NavigationItem { Title = "Job Orders", Icon = "clipboard-list", Url = "/Billing/JobOrders" },
                    new NavigationItem { Title = "Invoices", Icon = "file-invoice", Url = "/Billing/Invoices" },
                    new NavigationItem { Title = "Payments", Icon = "credit-card", Url = "/Billing/Payments" },
                    new NavigationItem { Title = "Adjustments", Icon = "sliders", Url = "/Billing/Adjustments" },
                    new NavigationItem { Title = "Archive", Icon = "archive", Url = "/Archive" }
                }
            },
            new NavigationSection
            {
                Title = "Insights",
                Items = new[]
                {
                    new NavigationItem { Title = "Reports", Icon = "bar-chart-2", Url = "/Billing/Reports" }
                }
            }
        };
    }

    private IEnumerable<NavigationSection> GetTechnicianNavigation()
    {
        return new[]
        {
            new NavigationSection
            {
                Title = "Overview",
                Items = new[]
                {
                    new NavigationItem { Title = "Dashboard", Icon = "home", Url = "/Technician/Dashboard" }
                }
            },
            new NavigationSection
            {
                Title = "Work",
                Items = new[]
                {
                    new NavigationItem { Title = "My Job Orders", Icon = "clipboard-list", Url = "/Technician/JobOrders" },
                    new NavigationItem { Title = "Parts Usage", Icon = "package", Url = "/Technician/PartsUsage" }
                }
            }
        };
    }

    private IEnumerable<NavigationSection> GetAuditorNavigation()
    {
        return new[]
        {
            new NavigationSection
            {
                Title = "Overview",
                Items = new[]
                {
                    new NavigationItem { Title = "Dashboard", Icon = "home", Url = "/Auditor/Dashboard" }
                }
            },
            new NavigationSection
            {
                Title = "Review (Read-Only)",
                Items = new[]
                {
                    new NavigationItem { Title = "Invoices", Icon = "file-invoice", Url = "/Auditor/Invoices" },
                    new NavigationItem { Title = "Payments", Icon = "credit-card", Url = "/Auditor/Payments" },
                    new NavigationItem { Title = "Adjustments", Icon = "sliders", Url = "/Auditor/Adjustments" }
                }
            },
            new NavigationSection
            {
                Title = "Insights",
                Items = new[]
                {
                    new NavigationItem { Title = "Reports", Icon = "bar-chart-2", Url = "/Auditor/Reports" }
                }
            },
            new NavigationSection
            {
                Title = "System",
                Items = new[]
                {
                    new NavigationItem { Title = "Audit Logs", Icon = "shield", Url = "/Auditor/AuditLogs" }
                }
            }
        };
    }
}

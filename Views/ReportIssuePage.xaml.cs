using Fix_It.Models;
using Fix_It.ViewModels;

namespace Fix_It.Views
{
    public partial class ReportIssuePage : ContentPage
    {
        // existingReport is null for a brand-new report; supplied when reached via
        // IssueDetailPage's Edit button.
        public ReportIssuePage(User currentUser, IssueReport? existingReport = null)
        {
            InitializeComponent();

            BindingContext = new ReportIssueViewModel(this, currentUser, existingReport);
        }
    }
}

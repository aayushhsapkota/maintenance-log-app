using Fix_It.Models;
using Fix_It.Services;
using Fix_It.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Fix_It.Views
{
    public partial class ReportIssuePage : ContentPage
    {
        public ReportIssuePage(User currentUser)
        {
            InitializeComponent();

            var databaseService = IPlatformApplication.Current!.Services.GetRequiredService<DatabaseService>();
            BindingContext = new ReportIssueViewModel(databaseService, this, currentUser.Id);
        }
    }
}

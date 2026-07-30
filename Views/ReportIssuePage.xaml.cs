using Fix_It.Models;
using Fix_It.ViewModels;

namespace Fix_It.Views
{
    public partial class ReportIssuePage : ContentPage
    {
        public ReportIssuePage(User currentUser)
        {
            InitializeComponent();

            BindingContext = new ReportIssueViewModel(this, currentUser.FirebaseUid);
        }
    }
}

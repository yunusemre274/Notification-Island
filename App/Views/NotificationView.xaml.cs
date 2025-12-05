using System.Windows.Controls;
using NI.App.ViewModels;

namespace NI.App.Views
{
    public partial class NotificationView : UserControl
    {
        public NotificationView()
        {
            InitializeComponent();
            var vm = (NotificationViewModel)DataContext;
            vm.Start();
        }
    }
}

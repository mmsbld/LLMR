using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LLMR.Views.Tabs
{
    public partial class DataCollectionTab : UserControl
    {
        public DataCollectionTab()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
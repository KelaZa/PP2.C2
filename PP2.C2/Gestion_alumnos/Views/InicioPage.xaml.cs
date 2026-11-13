using Gestion_alumnos.ViewModels;

namespace Gestion_alumnos
{
    public partial class InicioPage : ContentPage
    {
        private readonly InicioViewModel viewModel;

        public InicioPage(InicioViewModel viewModel)
        {
            InitializeComponent();
            this.viewModel = viewModel;
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await viewModel.CargarResumenAsync();
        }
    }
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Gestion_alumnos.Servicios;

namespace Gestion_alumnos.ViewModels
{
    public class InicioViewModel : INotifyPropertyChanged
    {
        private const string MensajeSinEstudiantes =
            "Todavía no hay datos suficientes. Registre estudiantes para comenzar con el seguimiento académico.";

        private readonly DatosService datosService;
        private readonly RiesgoService riesgoService;
        private readonly SemaphoreSlim cargaLock = new(1, 1);
        private string? comisionSeleccionada;
        private bool mostrarEstadoVacio;
        private bool mostrarMetricas;
        private string textoEstadoVacio = MensajeSinEstudiantes;
        private string riesgoBajoTexto = "0";
        private string riesgoMedioTexto = "0";
        private string riesgoAltoTexto = "0";
        private string asistenciaPromedioTexto = "0,0 %";
        private string tasaEntregaTexto = "0,0 %";
        private string intervencionesContactadosTexto = "0";
        private string intervencionesPendientesTexto = "0";

        public IReadOnlyList<string> Comisiones => ComisionesService.Todas;

        public string? ComisionSeleccionada
        {
            get => comisionSeleccionada;
            set
            {
                if (comisionSeleccionada == value)
                    return;

                comisionSeleccionada = value;
                OnPropertyChanged();
                _ = ActualizarMetricasAsync();
            }
        }

        public bool MostrarEstadoVacio
        {
            get => mostrarEstadoVacio;
            private set => SetProperty(ref mostrarEstadoVacio, value);
        }

        public bool MostrarMetricas
        {
            get => mostrarMetricas;
            private set => SetProperty(ref mostrarMetricas, value);
        }

        public string TextoEstadoVacio
        {
            get => textoEstadoVacio;
            private set => SetProperty(ref textoEstadoVacio, value);
        }

        public string RiesgoBajoTexto
        {
            get => riesgoBajoTexto;
            private set => SetProperty(ref riesgoBajoTexto, value);
        }

        public string RiesgoMedioTexto
        {
            get => riesgoMedioTexto;
            private set => SetProperty(ref riesgoMedioTexto, value);
        }

        public string RiesgoAltoTexto
        {
            get => riesgoAltoTexto;
            private set => SetProperty(ref riesgoAltoTexto, value);
        }

        public string AsistenciaPromedioTexto
        {
            get => asistenciaPromedioTexto;
            private set => SetProperty(ref asistenciaPromedioTexto, value);
        }

        public string TasaEntregaTexto
        {
            get => tasaEntregaTexto;
            private set => SetProperty(ref tasaEntregaTexto, value);
        }

        public string IntervencionesContactadosTexto
        {
            get => intervencionesContactadosTexto;
            private set => SetProperty(ref intervencionesContactadosTexto, value);
        }

        public string IntervencionesPendientesTexto
        {
            get => intervencionesPendientesTexto;
            private set => SetProperty(ref intervencionesPendientesTexto, value);
        }

        public ICommand RegistrarPrimerAlumnoCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public InicioViewModel(DatosService datosService, RiesgoService riesgoService)
        {
            this.datosService = datosService;
            this.riesgoService = riesgoService;
            RegistrarPrimerAlumnoCommand = new Command(async () => await IrAEstudiantesAsync());

            datosService.Estudiantes.CollectionChanged += (_, _) => _ = ActualizarMetricasAsync();
            datosService.Asistencias.CollectionChanged += (_, _) => _ = ActualizarMetricasAsync();
            datosService.Entregas.CollectionChanged += (_, _) => _ = ActualizarMetricasAsync();
            datosService.Intervenciones.CollectionChanged += (_, _) => _ = ActualizarMetricasAsync();
        }

        public async Task CargarResumenAsync()
        {
            await cargaLock.WaitAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(comisionSeleccionada) && Comisiones.Count > 0)
                {
                    comisionSeleccionada = Comisiones[0];
                    OnPropertyChanged(nameof(ComisionSeleccionada));
                }

                ActualizarEstadoVacio();
                ActualizarMetricas();
            }
            finally
            {
                cargaLock.Release();
            }
        }

        private async Task ActualizarMetricasAsync()
        {
            await cargaLock.WaitAsync();
            try
            {
                ActualizarEstadoVacio();
                ActualizarMetricas();
            }
            finally
            {
                cargaLock.Release();
            }
        }

        private void ActualizarEstadoVacio()
        {
            MostrarEstadoVacio = datosService.Estudiantes.Count == 0;
            MostrarMetricas = !MostrarEstadoVacio;
            TextoEstadoVacio = MensajeSinEstudiantes;
        }

        private void ActualizarMetricas()
        {
            if (MostrarEstadoVacio || string.IsNullOrWhiteSpace(ComisionSeleccionada))
                return;

            var riesgos = riesgoService.CalcularRiesgos()
                .Where(r => r.Comision == ComisionSeleccionada)
                .ToList();

            RiesgoBajoTexto = riesgos.Count(r => r.NivelRiesgo == "Bajo").ToString();
            RiesgoMedioTexto = riesgos.Count(r => r.NivelRiesgo == "Medio").ToString();
            RiesgoAltoTexto = riesgos.Count(r => r.NivelRiesgo == "Alto").ToString();

            var asistencias = datosService.Asistencias
                .Where(a => a.Comision == ComisionSeleccionada)
                .ToList();
            var porcentajeAsistencia = asistencias.Count == 0
                ? 0
                : asistencias.Count(a => a.Presente) * 100d / asistencias.Count;
            AsistenciaPromedioTexto = FormatearPorcentaje(porcentajeAsistencia);

            var entregas = datosService.Entregas
                .Where(e => e.Comision == ComisionSeleccionada)
                .ToList();
            var tasaEntrega = entregas.Count == 0
                ? 0
                : entregas.Count(e => e.Entregado) * 100d / entregas.Count;
            TasaEntregaTexto = FormatearPorcentaje(tasaEntrega);

            var estudiantesEnRiesgo = riesgos
                .Where(r => r.NivelRiesgo == "Medio" || r.NivelRiesgo == "Alto")
                .Select(r => r.Legajo)
                .ToHashSet();
            var contactados = datosService.Intervenciones
                .Where(i => estudiantesEnRiesgo.Contains(i.LegajoEstudiante))
                .Select(i => i.LegajoEstudiante)
                .Distinct()
                .Count();

            IntervencionesContactadosTexto = contactados.ToString();
            IntervencionesPendientesTexto = Math.Max(estudiantesEnRiesgo.Count - contactados, 0).ToString();
        }

        private static string FormatearPorcentaje(double valor)
        {
            return $"{valor:F1} %";
        }

        private static async Task IrAEstudiantesAsync()
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("//Estudiantes");
        }

        private void SetProperty<T>(ref T campo, T valor, [CallerMemberName] string? nombre = null)
        {
            if (EqualityComparer<T>.Default.Equals(campo, valor))
                return;

            campo = valor;
            OnPropertyChanged(nombre);
        }

        private void OnPropertyChanged([CallerMemberName] string? nombre = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
        }
    }
}

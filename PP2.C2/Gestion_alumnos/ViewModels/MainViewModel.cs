using Gestion_alumnos.Modelos;
using Gestion_alumnos.Servicios;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace Gestion_alumnos.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DatosService datosService;

        private bool importandoCsv;
        private bool suspendiendoActualizacionNomina;

        public IReadOnlyList<string> Comisiones => ComisionesService.Todas;
        public ObservableCollection<Estudiante> Estudiantes { get; }

        public ObservableCollection<Estudiante> EstudiantesVisibles { get; }

        // DATOS DEL FORMULARIO
        private string apellido = "";

        public string Apellido
        {
            get { return apellido; }
            set
            {
                apellido = value;
                OnPropertyChanged();
            }
        }

        private string nombre = "";

        public string Nombre
        {
            get { return nombre; }
            set
            {
                nombre = value;
                OnPropertyChanged();
            }
        }

        private string legajo = "";

        public string Legajo
        {
            get { return legajo; }
            set
            {
                legajo = value;
                OnPropertyChanged();
            }
        }

        private string? comisionSeleccionada;

        public string? ComisionSeleccionada
        {
            get { return comisionSeleccionada; }
            set
            {
                comisionSeleccionada = value;
                OnPropertyChanged();
                ActualizarNomina(value);
            }
        }

        // DATOS QUE MUESTRA LA PANTALLA
        private string totalTexto = "Seleccione una comisión";

        public string TotalTexto
        {
            get { return totalTexto; }
            set
            {
                totalTexto = value;
                OnPropertyChanged();
            }
        }

        private bool mostrarMensajeVacio;

        public bool MostrarMensajeVacio
        {
            get { return mostrarMensajeVacio; }
            set
            {
                mostrarMensajeVacio = value;
                OnPropertyChanged();
            }
        }

        // COMANDO PARA AGREGAR ESTUDIANTES
        public ICommand AgregarEstudianteCommand { get; }

        // COMANDO PARA IMPORTAR ESTUDIANTES DESDE CSV
        public ICommand ImportarCsvCommand { get; }

        // NOTIFICACIÓN DE CAMBIOS
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? nombrePropiedad = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombrePropiedad));
        }

        // CONSTRUCTOR
        public MainViewModel(DatosService datosService)
        {
            this.datosService = datosService;

            Estudiantes = datosService.Estudiantes;

            EstudiantesVisibles = new ObservableCollection<Estudiante>();

            AgregarEstudianteCommand = new Command(async () => await AgregarEstudianteAsync());
            ImportarCsvCommand = new Command(async () => await ImportarCsvAsync());

            Estudiantes.CollectionChanged += (sender, args) =>
            {
                if (!suspendiendoActualizacionNomina)
                {
                    ActualizarNomina(ComisionSeleccionada);
                }
            };
        }

        // AGREGAR ESTUDIANTE
        private async Task AgregarEstudianteAsync()
        {
            if (string.IsNullOrWhiteSpace(Apellido))
            {
                await MostrarError("Error","El apellido no puede estar vacío.");
                return;
            }

            if (string.IsNullOrWhiteSpace(Nombre))
            {
                await MostrarError("Error","El nombre no puede estar vacío.");
                return;
            }

            if (!int.TryParse(Legajo, out int numeroLegajo) || numeroLegajo <= 0)
            {
                await MostrarError("Error","El legajo debe ser un número positivo válido.");
                return;
            }

            // VERIFICAR LEGAJO REPETIDO
            foreach (Estudiante estudiante in Estudiantes)
            {
                if (estudiante.Legajo == numeroLegajo)
                {
                    await MostrarError("Legajo repetido",$"El legajo {numeroLegajo} ya pertenece a " + $"{estudiante.Nombre} {estudiante.Apellido}.");
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(ComisionSeleccionada))
            {
                await MostrarError("Error","Debe seleccionar una comisión.");
                return;
            }

            // CREAR ESTUDIANTE
            Estudiante nuevoEstudiante = new Estudiante
            {
                Apellido = Apellido.Trim(),
                Nombre = Nombre.Trim(),
                Legajo = numeroLegajo,
                Comision = ComisionSeleccionada
            };

            try
            {
                await datosService.GuardarEstudianteAsync(nuevoEstudiante);
            }
            catch (Exception ex)
            {
                await MostrarError("Error de SQLite",$"No se pudo guardar el estudiante: {ex.Message}");
                return;
            }

            // ACTUALIZAR NÓMINA
            ActualizarNomina(ComisionSeleccionada);

            // LIMPIAR CAMPOS
            Apellido = "";
            Nombre = "";
            Legajo = "";
        }

        // IMPORTAR ESTUDIANTES DESDE UN ARCHIVO CSV
        private async Task ImportarCsvAsync()
        {
            if (importandoCsv)
            {
                return;
            }

            string? comision = ComisionSeleccionada;

            if (string.IsNullOrWhiteSpace(comision))
            {
                await MostrarError(
                    "Comisión requerida",
                    "Debe seleccionar una comisión antes de importar estudiantes.");
                return;
            }

            importandoCsv = true;

            try
            {
                PickOptions opciones = CrearOpcionesArchivoCsv();
                FileResult? archivo = await FilePicker.Default.PickAsync(opciones);

                if (archivo == null)
                {
                    return;
                }

                string extension = Path.GetExtension(archivo.FileName);

                if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
                {
                    await MostrarError(
                        "Archivo no válido",
                        "El archivo seleccionado debe tener extensión .csv.");
                    return;
                }

                ResultadoImportacionCsv resultado =
                    await LeerEstudiantesCsvAsync(archivo, comision);

                if (resultado.Estudiantes.Count == 0)
                {
                    string detalle = resultado.FilasOmitidas switch
                    {
                        0 => "El archivo no contiene estudiantes para importar.",
                        1 => "No se encontraron filas válidas. Se omitió 1 fila.",
                        _ => $"No se encontraron filas válidas. Se omitieron {resultado.FilasOmitidas} filas."
                    };

                    await MostrarAlerta("Importación sin resultados", detalle);
                    return;
                }

                int importados;

                suspendiendoActualizacionNomina = true;

                try
                {
                    importados = await datosService.ImportarEstudiantesAsync(
                        resultado.Estudiantes);
                }
                finally
                {
                    suspendiendoActualizacionNomina = false;
                }

                ActualizarNomina(comision);

                string mensaje = importados == 1
                    ? $"Se importó 1 estudiante en la comisión {comision}."
                    : $"Se importaron {importados} estudiantes en la comisión {comision}.";

                if (resultado.FilasOmitidas == 1)
                {
                    mensaje +=
                        "\nSe omitió 1 fila por datos inválidos o legajos repetidos.";
                }
                else if (resultado.FilasOmitidas > 1)
                {
                    mensaje +=
                        $"\nSe omitieron {resultado.FilasOmitidas} filas por datos inválidos o legajos repetidos.";
                }

                await MostrarAlerta("Importación completada", mensaje);
            }
            catch (Exception ex)
            {
                await MostrarError(
                    "Error al importar CSV",
                    $"No se pudo completar la importación: {ex.Message}");
            }
            finally
            {
                importandoCsv = false;
            }
        }

        private static PickOptions CrearOpcionesArchivoCsv()
        {
            return new PickOptions
            {
                PickerTitle = "Seleccione un archivo CSV",
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".csv" } },
                        { DevicePlatform.Android, new[] { ".csv" } },
                        { DevicePlatform.iOS, new[] { ".csv" } },
                        { DevicePlatform.MacCatalyst, new[] { ".csv" } }
                    })
            };
        }

        private async Task<ResultadoImportacionCsv> LeerEstudiantesCsvAsync(
            FileResult archivo,
            string comision)
        {
            List<string> lineas;

            try
            {
                lineas = await LeerLineasAsync(
                    archivo,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false,
                        throwOnInvalidBytes: true));
            }
            catch (DecoderFallbackException)
            {
                lineas = await LeerLineasAsync(archivo, Encoding.Latin1);
            }

            ResultadoImportacionCsv resultado = new();
            HashSet<int> legajosExistentes = Estudiantes
                .Select(estudiante => estudiante.Legajo)
                .ToHashSet();
            HashSet<int> legajosArchivo = new();
            bool primeraLinea = true;

            foreach (string linea in lineas)
            {
                if (string.IsNullOrWhiteSpace(linea))
                {
                    continue;
                }

                char delimitador = DetectarDelimitador(linea);
                List<string> campos = ParsearLineaCsv(linea, delimitador);

                if (primeraLinea)
                {
                    primeraLinea = false;

                    if (EsCabecera(campos))
                    {
                        continue;
                    }
                }

                if (campos.Count < 3)
                {
                    resultado.FilasOmitidas++;
                    continue;
                }

                if (!int.TryParse(campos[0].Trim(), out int legajo) || legajo <= 0)
                {
                    resultado.FilasOmitidas++;
                    continue;
                }

                string apellido = campos[1].Trim();
                string nombre = campos[2].Trim();

                if (string.IsNullOrWhiteSpace(apellido) ||
                    string.IsNullOrWhiteSpace(nombre))
                {
                    resultado.FilasOmitidas++;
                    continue;
                }

                if (legajosExistentes.Contains(legajo) ||
                    !legajosArchivo.Add(legajo))
                {
                    resultado.FilasOmitidas++;
                    continue;
                }

                resultado.Estudiantes.Add(new Estudiante
                {
                    Legajo = legajo,
                    Apellido = apellido,
                    Nombre = nombre,
                    Comision = comision
                });
            }

            return resultado;
        }

        private static async Task<List<string>> LeerLineasAsync(
            FileResult archivo,
            Encoding codificacion)
        {
            List<string> lineas = new();

            using Stream stream = await archivo.OpenReadAsync();
            using StreamReader lector = new(
                stream,
                codificacion,
                detectEncodingFromByteOrderMarks: true);

            string? linea;

            while ((linea = await lector.ReadLineAsync()) != null)
            {
                lineas.Add(linea);
            }

            return lineas;
        }

        private static bool EsCabecera(IReadOnlyList<string> campos)
        {
            if (campos.Count == 0)
            {
                return false;
            }

            string primerCampo = campos[0]
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty);

            return primerCampo.Contains(
                "legajo",
                StringComparison.OrdinalIgnoreCase);
        }

        private static char DetectarDelimitador(string linea)
        {
            int puntoYComa = 0;
            int coma = 0;
            bool entreComillas = false;

            for (int i = 0; i < linea.Length; i++)
            {
                char caracter = linea[i];

                if (caracter == '"')
                {
                    if (entreComillas &&
                        i + 1 < linea.Length &&
                        linea[i + 1] == '"')
                    {
                        i++;
                    }
                    else
                    {
                        entreComillas = !entreComillas;
                    }
                }
                else if (!entreComillas)
                {
                    if (caracter == ';')
                    {
                        puntoYComa++;
                    }
                    else if (caracter == ',')
                    {
                        coma++;
                    }
                }
            }

            return puntoYComa >= coma ? ';' : ',';
        }

        private static List<string> ParsearLineaCsv(
            string linea,
            char delimitador)
        {
            List<string> campos = new();
            StringBuilder campo = new();
            bool entreComillas = false;

            for (int i = 0; i < linea.Length; i++)
            {
                char caracter = linea[i];

                if (caracter == '"')
                {
                    if (entreComillas &&
                        i + 1 < linea.Length &&
                        linea[i + 1] == '"')
                    {
                        campo.Append('"');
                        i++;
                    }
                    else
                    {
                        entreComillas = !entreComillas;
                    }
                }
                else if (caracter == delimitador && !entreComillas)
                {
                    campos.Add(campo.ToString().Trim());
                    campo.Clear();
                }
                else
                {
                    campo.Append(caracter);
                }
            }

            campos.Add(campo.ToString().Trim());

            return campos;
        }

        // MOSTRAR MENSAJES DE VALIDACIÓN
        private async Task MostrarError(string titulo, string mensaje)
        {
            await MostrarAlerta(titulo, mensaje);
        }

        private async Task MostrarAlerta(string titulo, string mensaje)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.DisplayAlertAsync(titulo, mensaje, "OK");
            }
        }

        private sealed class ResultadoImportacionCsv
        {
            public List<Estudiante> Estudiantes { get; } = new();

            public int FilasOmitidas { get; set; }
        }

        // FILTRAR Y ORDENAR ESTUDIANTES
        public void ActualizarNomina(string? comision)
        {
            EstudiantesVisibles.Clear();

            if (string.IsNullOrWhiteSpace(comision))
            {
                TotalTexto = "Seleccione una comisión";
                MostrarMensajeVacio = false;
                return;
            }

            var filtrados = Estudiantes
                .Where(e => e.Comision == comision)
                .OrderBy(e => e.Apellido)
                .ThenBy(e => e.Nombre)
                .ToList();

            foreach (Estudiante estudiante in filtrados)
            {
                EstudiantesVisibles.Add(estudiante);
            }

            TotalTexto = $"Total: {EstudiantesVisibles.Count} estudiantes";

                MostrarMensajeVacio =
                EstudiantesVisibles.Count == 0;
        }
    }
}

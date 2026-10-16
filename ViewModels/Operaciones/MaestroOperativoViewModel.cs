namespace ITServiceDeskApp.ViewModels.Operaciones;

public sealed class MaestroOperativoViewModel
{
    public bool Equipos { get; set; }
    public string Titulo => Equipos ? "Maestro de Equipos" : "Maestro de Conductores";
    public string[] Columnas => Equipos
        ? new[] { "Codigo", "Placa", "Tipo", "Marca", "Modelo" }
        : new[] { "Cedula", "Nombre", "Licencia", "Categoria", "Telefono" };
    public List<string[]> Registros { get; set; } = new();
    public List<string> Errores { get; set; } = new();
}

using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using ITServiceDeskApp.ViewModels.Operaciones;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
namespace ITServiceDeskApp.Controllers;
public partial class OperacionesController
{
    private static readonly IReadOnlyDictionary<string, string> HsTrainingDocumentFields = new Dictionary<string, string> {
        ["Inducción General"] = "induccion_general_fecha", ["Prevención de Incendios"] = "prevencion_incendios_fecha",
        ["Inducción de Sitio"] = "sitio_fecha", ["ATS"] = "ats_fecha", ["IPER"] = "iper_fecha",
        ["Operación de Traslado Mina a Mina"] = "oper_traslado_mina_fecha", ["Manejo de Residuos Sólidos"] = "manejo_residuos_solidos_fecha",
        ["Uso de Formato de Transporte"] = "uso_formato_transporte_fecha", ["Manejo de Hidrocarburos"] = "manejo_hidrocarburos_fecha",
        ["Educación Vial"] = "educacion_vial_fecha", ["Inducción Sistema de Gestión Social"] = "sistema_gestion_social_fecha",
        ["Manejo Defensivo Pesado"] = "manejo_defensivo_pesado_fecha", ["Gestión de Fatiga"] = "gestion_fatiga_fecha",
        ["Manejo Defensivo Liviano"] = "manejo_defensivo_liviano_fecha", ["Sistema 5 puntos"] = "sistema_5_puntos_fecha",
        ["Respuesta ante emergencias"] = "respuestas_emergencia_fecha", ["Primeros Auxilios Teórico"] = "primeros_auxilios_tecnico_fecha",
        ["Primeros Auxilios Práctico"] = "primeros_auxilios_practico_fecha"
    };

    // Use the immutable master ID for new documents; exact identifiers support older documents.
    private bool DocumentMatchesMaster(OperationalMasterRecord master, string type, string name, Dictionary<string, string[]> payload, IReadOnlyList<OperationalMasterRecord>? catalog = null)
    {
        var person = master.Kind == "conductores";
        if (!person && (master.Kind == "equipos") != type.Equals("Ingreso de Equipo", StringComparison.OrdinalIgnoreCase)) return false;
        var linked = TryReadPositiveInt(FirstPayloadValue(payload, "master_record_id"));
        var identifier = person ? FirstPayloadValue(payload, "nombres_apellidos", "nombre_completo", "nombre")
            : master.Kind == "equipos" ? FirstPayloadValue(payload, "equipo_codigo_cabezal") : FirstPayloadValue(payload, "gondola_placa", "equipo_placa_gondola");
        if (string.IsNullOrWhiteSpace(identifier)) identifier = name;
        catalog ??= _context.OperationalMasterRecords.AsNoTracking().Where(x => x.Kind == master.Kind).ToList();
        return OperationalMasterIdentity.Resolve(catalog, master.Kind, linked, identifier,
            FirstPayloadValue(payload, "equipo_placa_cabezal"))?.Id == master.Id;
    }

    private DocumentMasterFormViewModel BuildDocumentMasterForm(OperationalMasterRecord? master, int? requestId, bool person)
    {
        var catalog = _context.OperationalMasterRecords.AsNoTracking().ToList();
        var model = new DocumentMasterFormViewModel { MasterId = master?.Id, DisplayId = master?.DisplayId ?? "", Name = master?.Name ?? "", Kind = master?.Kind ?? "", Tenure = master?.Tenure ?? "", Owner = master?.Owner ?? "", Type = person ? "1. Solicitud Ingreso" : master?.Kind == "gondolas" ? "Ingreso de Gondola" : "Ingreso de Equipo" };
        if (person)
        {
            var requests = _context.OperacionesIngresoPersonalSolicitudes.AsNoTracking().OrderByDescending(x => x.UpdatedAt).ToList();
            var request = requestId.HasValue ? requests.FirstOrDefault(x => x.Id == requestId) : master == null ? null : requests.FirstOrDefault(x => DocumentMatchesMaster(master, x.Tipo, x.SolicitanteNombre, DeserializeEquipoGondolaPayload(x.PayloadJson), catalog));
            if (request != null && (master == null || DocumentMatchesMaster(master, request.Tipo, request.SolicitanteNombre, DeserializeEquipoGondolaPayload(request.PayloadJson), catalog))) { model.RequestId = request.Id; model.Type = request.Tipo; model.Payload = DeserializeEquipoGondolaPayload(request.PayloadJson); }
        }
        else
        {
            var requests = _context.OperacionesIngresoEquipoGondolaSolicitudes.AsNoTracking().OrderByDescending(x => x.UpdatedAt).ToList();
            var request = requestId.HasValue ? requests.FirstOrDefault(x => x.Id == requestId) : master == null ? null : requests.FirstOrDefault(x => DocumentMatchesMaster(master, x.Tipo, x.Asunto, DeserializeEquipoGondolaPayload(x.PayloadJson), catalog));
            if (request != null && (master == null || DocumentMatchesMaster(master, request.Tipo, request.Asunto, DeserializeEquipoGondolaPayload(request.PayloadJson), catalog))) { model.RequestId = request.Id; model.Type = request.Tipo; model.Payload = DeserializeEquipoGondolaPayload(request.PayloadJson); }
        }
        if (master != null) ApplyMasterIdentity(master, model.Payload, person);
        return model;
    }

    private static void ApplyMasterIdentity(OperationalMasterRecord master, Dictionary<string, string[]> payload, bool person)
    {
        payload["master_record_id"] = [master.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)];
        if (!payload.ContainsKey("asunto") || string.IsNullOrWhiteSpace(FirstPayloadValue(payload, "asunto"))) payload["asunto"] = [master.Name];
        if (person) payload["nombres_apellidos"] = [master.Name];
        else if (master.Kind == "equipos")
        {
            payload["equipo_codigo_cabezal"] = [master.Name]; payload["equipo_placa_cabezal"] = [master.Plate ?? ""];
            payload["equipo_tenencia"] = [master.Tenure]; payload["equipo_propietario"] = [master.Owner];
        }
        else payload["gondola_placa"] = [master.Name];
    }

    private IActionResult DocumentMasterForm(int? masterId, int? requestId, bool person, string? kind = null)
    {
        if (!person && kind != null && kind is not ("equipos" or "gondolas")) return NotFound();
        ViewData["DocumentMasterKind"] = kind;
        ViewData["DocumentMasterPerson"] = person;
        ViewData["DocumentMasterChoices"] = _context.OperationalMasterRecords.AsNoTracking().Where(x => x.IsActive && (kind == null || x.Kind == kind) && (person ? x.Kind == "conductores" : x.Kind == "equipos" || x.Kind == "gondolas")).OrderBy(x => x.Kind).ThenBy(x => x.Name).ToList();
        var master = masterId.HasValue ? _context.OperationalMasterRecords.AsNoTracking().SingleOrDefault(x => x.Id == masterId && (kind == null || x.Kind == kind) && (person ? x.Kind == "conductores" : x.Kind == "equipos" || x.Kind == "gondolas")) : null;
        if (masterId.HasValue && master == null) return NotFound();
        if (masterId.HasValue || requestId.HasValue)
        {
            var model = BuildDocumentMasterForm(master, requestId, person);
            if (requestId.HasValue && model.RequestId == null) return NotFound();
            if (!person && kind != null && model.RequestId.HasValue && model.Type != (kind == "equipos" ? "Ingreso de Equipo" : "Ingreso de Gondola")) return NotFound();
            if (master != null && !master.IsActive && model.RequestId == null) return NotFound();
            ViewData["DocumentMasterForm"] = model;
        }
        return View(person ? "NuevoIngresoPersonal" : "NuevoIngresoEquipoGondola");
    }

    private bool TryBindDocumentMaster(IFormCollection input, bool person, out IFormCollection bound, out string error)
    {
        bound = input; error = "";
        var masterText = input["master_record_id"].ToString();
        var requestId = TryReadPositiveInt(input["solicitud_id"].ToString());
        var oldPayload = new Dictionary<string, string[]>();
        var oldName = ""; var oldType = "";
        if (requestId.HasValue)
        {
            var request = person ? _context.OperacionesIngresoPersonalSolicitudes.AsNoTracking().Where(x => x.Id == requestId).Select(x => new { x.PayloadJson, Name = x.SolicitanteNombre, x.Tipo }).SingleOrDefault()
                : _context.OperacionesIngresoEquipoGondolaSolicitudes.AsNoTracking().Where(x => x.Id == requestId).Select(x => new { x.PayloadJson, Name = x.Asunto, x.Tipo }).SingleOrDefault();
            if (request == null) { error = "No se encontró la solicitud para actualizar."; return false; }
            oldPayload = DeserializeEquipoGondolaPayload(request.PayloadJson); oldName = request.Name; oldType = request.Tipo;
            var oldMaster = FirstPayloadValue(oldPayload, "master_record_id");
            if (string.IsNullOrWhiteSpace(masterText)) masterText = oldMaster;
            else if (!string.IsNullOrWhiteSpace(oldMaster) && oldMaster != masterText) { error = "La solicitud pertenece a otro registro maestro."; return false; }
        }
        if (string.IsNullOrWhiteSpace(masterText)) return true;
        if (!int.TryParse(masterText, out var masterId)) { error = "El ID del maestro no es válido."; return false; }
        var master = _context.OperationalMasterRecords.AsNoTracking().SingleOrDefault(x => x.Id == masterId && (person ? x.Kind == "conductores" : x.Kind == "equipos" || x.Kind == "gondolas"));
        if (master == null || (!master.IsActive && !requestId.HasValue)) { error = "El registro maestro no está disponible para una nueva solicitud."; return false; }
        var tipo = input[person ? "tipo" : "tipo_ingreso"].ToString();
        if (!person && tipo != (master.Kind == "equipos" ? "Ingreso de Equipo" : "Ingreso de Gondola")) { error = "El tipo de ingreso no corresponde al maestro seleccionado."; return false; }
        if (requestId.HasValue && !DocumentMatchesMaster(master, oldType, oldName, oldPayload)) { error = "La solicitud no corresponde al maestro seleccionado."; return false; }
        var existing = BuildDocumentMasterForm(master, requestId, person);
        var payload = new Dictionary<string, string[]>(existing.Payload, StringComparer.OrdinalIgnoreCase);
        foreach (var field in input) payload[field.Key] = field.Value.Select(v => v ?? "").ToArray();
        ApplyMasterIdentity(master, payload, person);
        if (!requestId.HasValue && existing.RequestId.HasValue)
            payload["solicitud_id"] = [existing.RequestId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)];
        bound = new FormCollection(payload.ToDictionary(x => x.Key, x => new StringValues(x.Value), StringComparer.OrdinalIgnoreCase));
        return true;
    }
}

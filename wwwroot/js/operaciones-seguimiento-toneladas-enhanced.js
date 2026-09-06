document.addEventListener("DOMContentLoaded", function () {
    var root = document.getElementById("ops-enhanced-report");
    var seedScript = document.getElementById("ops-analytics-seed");
    if (!root || !seedScript) {
        return;
    }

    var seed;
    try {
        seed = JSON.parse(seedScript.textContent || "{}");
    } catch (_error) {
        return;
    }

    var rows = Array.isArray(seed.rows)
        ? seed.rows.map(function (row) {
            var parsedDate = row && row.fecha ? new Date(row.fecha + "T00:00:00") : null;
            if (parsedDate && Number.isNaN(parsedDate.getTime())) {
                parsedDate = null;
            }

            return {
                fecha: parsedDate,
                procedencia: (function () {
                    var normalized = ((row && row.procedencia) || "").toUpperCase().trim();
                    return normalized === "PAVON ASM" ? "TRITON" : normalized;
                })(),
                estado: ((row && row.estado) || "OTRO").toUpperCase(),
                equipo: ((row && row.equipo) || "").trim(),
                toneladas: Number((row && row.toneladas) || 0),
                toneladasRuta: Number((row && row.toneladasRuta) || 0),
                viajes: Number((row && row.viajes) || 0),
                incompleto: !!(row && row.incompleto)
            };
        })
        : [];

    var nf = new Intl.NumberFormat("es-NI", { minimumFractionDigits: 1, maximumFractionDigits: 1 });
    var ni = new Intl.NumberFormat("es-NI", { maximumFractionDigits: 0 });
    var dateLabel = new Intl.DateTimeFormat("es-NI", { day: "2-digit", month: "2-digit", year: "numeric" });
    var dayLabel = new Intl.DateTimeFormat("es-NI", { day: "2-digit", month: "2-digit" });

    var metaDiariaTriton = Number(seed.metaDiariaTriton || 0);
    var metaMensualTriton = Number(seed.metaMensualTriton || 0);
    var currentRange = "hoy";
    var currentCustomBounds = null;
    var customRangeFromInput = document.getElementById("customRangeFrom");
    var customRangeToInput = document.getElementById("customRangeTo");
    var applyCustomRangeBtn = document.getElementById("applyCustomRange");
    var resetCustomRangeBtn = document.getElementById("resetCustomRange");

    function startOfDay(date) {
        return new Date(date.getFullYear(), date.getMonth(), date.getDate());
    }

    function addDays(date, days) {
        var cloned = startOfDay(date);
        cloned.setDate(cloned.getDate() + days);
        return cloned;
    }

    function maxDateFromRows() {
        var dates = rows
            .filter(function (r) { return !!r.fecha; })
            .map(function (r) { return r.fecha.getTime(); });
        if (!dates.length) {
            return startOfDay(new Date());
        }

        return startOfDay(new Date(Math.max.apply(null, dates)));
    }

    function parseDateOrFallback(value, fallback) {
        if (!value) {
            return fallback;
        }

        var date = new Date(value + "T00:00:00");
        return Number.isNaN(date.getTime()) ? fallback : startOfDay(date);
    }

    function parseDateInput(value) {
        if (!value) {
            return null;
        }

        var parsed = new Date(value + "T00:00:00");
        return Number.isNaN(parsed.getTime()) ? null : startOfDay(parsed);
    }

    function formatDateInput(date) {
        var year = date.getFullYear();
        var month = String(date.getMonth() + 1).padStart(2, "0");
        var day = String(date.getDate()).padStart(2, "0");
        return year + "-" + month + "-" + day;
    }

    var cutoffBase = parseDateOrFallback(seed.fechaOperativa || seed.fechaCorte, maxDateFromRows());
    if (customRangeFromInput && !customRangeFromInput.value) {
        customRangeFromInput.value = formatDateInput(new Date(cutoffBase.getFullYear(), cutoffBase.getMonth(), 1));
    }

    if (customRangeToInput && !customRangeToInput.value) {
        customRangeToInput.value = formatDateInput(cutoffBase);
    }

    function resolveEquinoxStart(anchorDate) {
        var anchor = startOfDay(anchorDate);
        var monthOffset = anchor.getDate() >= 28 ? 0 : -1;
        var baseMonth = new Date(anchor.getFullYear(), anchor.getMonth() + monthOffset, 1);
        return new Date(baseMonth.getFullYear(), baseMonth.getMonth(), 28);
    }

    function resolveWeekStartSunday(anchorDate) {
        var anchor = startOfDay(anchorDate);
        return addDays(anchor, -anchor.getDay());
    }

    function resolveCustomBoundsFromInputs() {
        var from = parseDateInput(customRangeFromInput ? customRangeFromInput.value : "");
        var to = parseDateInput(customRangeToInput ? customRangeToInput.value : "");
        if (!from && !to) {
            return null;
        }

        if (!from) {
            from = to;
        } else if (!to) {
            to = from;
        }

        if (!from || !to) {
            return null;
        }

        if (from.getTime() > to.getTime()) {
            var swap = from;
            from = to;
            to = swap;
        }

        return { from: from, to: to };
    }

    function diffDaysInclusive(from, to) {
        var msPerDay = 24 * 60 * 60 * 1000;
        var diff = Math.floor((startOfDay(to).getTime() - startOfDay(from).getTime()) / msPerDay);
        return diff + 1;
    }

    function formatSigned(value, suffix) {
        var abs = Math.abs(value);
        var sign = value >= 0 ? "+" : "-";
        return sign + nf.format(abs) + (suffix || "");
    }

    function statusInfo(cumplimiento) {
        if (cumplimiento >= 100) {
            return { text: "Cumplida", css: "exec-status exec-status--ok" };
        }

        if (cumplimiento >= 90) {
            return { text: "En riesgo", css: "exec-status exec-status--warn" };
        }

        return { text: "Critica", css: "exec-status exec-status--danger" };
    }

    function getBounds(rangeKey) {
        var from;
        var to;
        var label;
        var key;

        if (rangeKey === "ayer") {
            from = addDays(cutoffBase, -1);
            to = addDays(cutoffBase, -1);
            label = "Ayer";
            key = "ayer";
        } else if (rangeKey === "semana") {
            from = resolveWeekStartSunday(cutoffBase);
            to = cutoffBase;
            label = "Semana";
            key = "semana";
        } else if (rangeKey === "mes") {
            from = new Date(cutoffBase.getFullYear(), cutoffBase.getMonth(), 1);
            to = cutoffBase;
            label = "Mes";
            key = "mes";
        } else if (rangeKey === "equinox") {
            from = resolveEquinoxStart(cutoffBase);
            to = cutoffBase;
            label = "Corte Equinox";
            key = "equinox";
        } else if (rangeKey === "custom" && currentCustomBounds) {
            from = currentCustomBounds.from;
            to = currentCustomBounds.to;
            label = "Rango personalizado";
            key = "custom";
        } else {
            from = cutoffBase;
            to = cutoffBase;
            label = "Hoy";
            key = "hoy";
        }

        return { from: from, to: to, label: label, key: key };
    }

    function rowsInBounds(bounds) {
        return rows.filter(function (r) {
            if (!r.fecha) {
                return false;
            }

            return r.fecha >= bounds.from && r.fecha <= bounds.to;
        });
    }

    function aggregateRange(rowsRange, bounds) {
        var finalizados = rowsRange.filter(function (r) { return r.estado === "FINALIZADO"; });
        var enRuta = rowsRange.filter(function (r) { return r.estado === "EN RUTA"; });
        var tritonTon = finalizados
            .filter(function (r) { return r.procedencia === "TRITON"; })
            .reduce(function (sum, item) { return sum + item.toneladas; }, 0);
        var pavonTon = 0;
        var totalTon = tritonTon;
        var metaDays = Math.max(1, diffDaysInclusive(bounds.from, bounds.to));
        var metaTriton = metaDiariaTriton * metaDays;
        var metaPavon = 0;
        var metaTotal = metaTriton;
        var cumplimiento = metaTotal > 0 ? (totalTon / metaTotal) * 100 : 0;
        var brecha = metaTotal - totalTon;
        var tritonCumpl = metaTriton > 0 ? (tritonTon / metaTriton) * 100 : 0;
        var pavonCumpl = 0;

        return {
            finalizados: finalizados,
            enRuta: enRuta,
            totalTon: totalTon,
            tritonTon: tritonTon,
            pavonTon: pavonTon,
            metaTotal: metaTotal,
            cumplimiento: cumplimiento,
            brecha: brecha,
            registros: rowsRange.length,
            finalizadosCount: finalizados.length,
            enRutaCount: enRuta.length,
            tritonCumpl: tritonCumpl,
            pavonCumpl: pavonCumpl
        };
    }

    function totalsForDay(targetDate) {
        var bounds = { from: targetDate, to: targetDate };
        var data = aggregateRange(rowsInBounds(bounds), bounds);
        return {
            total: data.totalTon,
            triton: data.tritonTon,
            pavon: data.pavonTon
        };
    }

    function averageLast7(anchorDate) {
        var list = [];
        for (var i = 6; i >= 0; i--) {
            list.push(totalsForDay(addDays(anchorDate, -i)));
        }

        var divisor = list.length || 1;
        var sumTotal = list.reduce(function (sum, item) { return sum + item.total; }, 0);
        var sumTriton = list.reduce(function (sum, item) { return sum + item.triton; }, 0);
        var sumPavon = list.reduce(function (sum, item) { return sum + item.pavon; }, 0);

        return {
            total: sumTotal / divisor,
            triton: sumTriton / divisor,
            pavon: sumPavon / divisor
        };
    }

    function setText(id, text) {
        var el = document.getElementById(id);
        if (el) {
            el.textContent = text;
        }
    }

    function renderTrend(anchorDate) {
        var container = document.getElementById("trend7Rows");
        if (!container) {
            return;
        }

        var points = [];
        for (var i = 6; i >= 0; i--) {
            var day = addDays(anchorDate, -i);
            var totals = totalsForDay(day);
            points.push({
                date: day,
                triton: totals.triton,
                pavon: totals.pavon,
                total: totals.total
            });
        }

        var maxValue = Math.max.apply(null, points.map(function (p) { return p.total; }).concat([1]));
        var html = points.map(function (point) {
            var tritonWidth = Math.max(2, (point.triton / maxValue) * 100);

            return [
                '<div class="trend-row">',
                '<div class="trend-row__date">' + dayLabel.format(point.date) + "</div>",
                '<div class="trend-row__bars">',
                '<div class="trend-track-mini"><div class="trend-bar-triton" style="width:' + tritonWidth.toFixed(2) + '%"></div></div>',
                "</div>",
                '<div class="trend-value">' + nf.format(point.total) + " Tn</div>",
                "</div>"
            ].join("");
        }).join("");

        container.innerHTML = html;
    }

    function resolveProgressBarCss(progressPct) {
        if (progressPct >= 100) {
            return "progress-bar";
        }

        if (progressPct >= 85) {
            return "progress-bar progress-bar--warn";
        }

        return "progress-bar progress-bar--danger";
    }

    function buildProgressItem(name, real, expected, monthlyMeta) {
        var avanceEsperado = expected > 0 ? (real / expected) * 100 : 0;
        var avanceEsperadoVisual = Math.max(0, Math.min(100, avanceEsperado));
        var cierreMensual = monthlyMeta > 0 ? (real / monthlyMeta) * 100 : 0;
        var cssClass = resolveProgressBarCss(avanceEsperado);

        return [
            '<div class="progress-item">',
            '<div class="progress-item__head">',
            '<span class="progress-item__name">' + name + "</span>",
            '<span class="progress-item__meta">' + nf.format(real) + " Tn</span>",
            "</div>",
            '<div class="progress-track"><div class="' + cssClass + '" style="width:' + avanceEsperadoVisual.toFixed(2) + '%"></div></div>',
            '<div class="progress-item__help">Esperada al corte: ' + nf.format(expected) + " Tn | Meta mes: " + nf.format(monthlyMeta) + " Tn | Cierre: " + nf.format(cierreMensual) + "%</div>",
            "</div>"
        ].join("");
    }

    function renderMonthlyProgress(anchorDate) {
        var wrap = document.getElementById("monthlyProgressWrap");
        if (!wrap) {
            return;
        }

        var monthRows = rows.filter(function (r) {
            return r.fecha &&
                r.fecha.getFullYear() === anchorDate.getFullYear() &&
                r.fecha.getMonth() === anchorDate.getMonth() &&
                r.fecha <= anchorDate &&
                r.estado === "FINALIZADO";
        });

        var realTriton = monthRows
            .filter(function (r) { return r.procedencia === "TRITON"; })
            .reduce(function (sum, item) { return sum + item.toneladas; }, 0);
        var realTotal = realTriton;

        var daysElapsed = anchorDate.getDate();
        var expectedTriton = metaDiariaTriton * daysElapsed;
        var expectedTotal = expectedTriton;
        var metaMensualTotal = metaMensualTriton;

        wrap.innerHTML = [
            buildProgressItem("TRITON", realTriton, expectedTriton, metaMensualTriton),
            buildProgressItem("TOTAL", realTotal, expectedTotal, metaMensualTotal)
        ].join("");
    }

    function groupEquipos(rowsRange) {
        var groups = {};
        rowsRange.forEach(function (row) {
            if (row.estado !== "FINALIZADO" || !row.equipo) {
                return;
            }

            if (!groups[row.equipo]) {
                groups[row.equipo] = { equipo: row.equipo, toneladas: 0, viajes: 0 };
            }

            groups[row.equipo].toneladas += row.toneladas;
            groups[row.equipo].viajes += row.viajes;
        });

        return Object.keys(groups).map(function (key) { return groups[key]; });
    }

    function renderEquiposTableRows(items) {
        if (!items.length) {
            return '<tr><td colspan="3" class="text-muted">Sin datos para este periodo.</td></tr>';
        }

        return items.map(function (item) {
            return [
                "<tr>",
                "<td>" + item.equipo + "</td>",
                '<td class="text-end fw-semibold">' + nf.format(item.toneladas) + "</td>",
                '<td class="text-end">' + ni.format(item.viajes) + "</td>",
                "</tr>"
            ].join("");
        }).join("");
    }

    function renderTopBottom(rowsRange) {
        var topBody = document.getElementById("topEquiposBody");
        var bottomBody = document.getElementById("bottomEquiposBody");
        if (!topBody || !bottomBody) {
            return;
        }

        var equipos = groupEquipos(rowsRange).sort(function (a, b) { return b.toneladas - a.toneladas; });
        var top = equipos.slice(0, 5);
        var bottom = equipos.slice().sort(function (a, b) { return a.toneladas - b.toneladas; }).slice(0, 5);

        topBody.innerHTML = renderEquiposTableRows(top);
        bottomBody.innerHTML = renderEquiposTableRows(bottom);
    }

    function renderAlerts(metrics, rowsRange) {
        var alertsList = document.getElementById("actionableAlertsList");
        if (!alertsList) {
            return;
        }

        var alerts = [];
        if (metrics.cumplimiento < 100) {
            alerts.push({
                level: "high",
                text: "Brecha de " + nf.format(Math.max(0, metrics.brecha)) + " Tn para cumplir la meta del periodo.",
                action: "Accion: priorizar carga y despacho en la procedencia con mayor rezago."
            });
        }

        if (metrics.enRutaCount > metrics.finalizadosCount) {
            alerts.push({
                level: "medium",
                text: "Hay mas registros en ruta (" + ni.format(metrics.enRutaCount) + ") que finalizados (" + ni.format(metrics.finalizadosCount) + ").",
                action: "Accion: revisar cuellos de botella en descarga y tiempos de patio."
            });
        }

        var equiposEnRutaSinCierre = [];
        var equipoMap = {};
        rowsRange.forEach(function (row) {
            if (!row.equipo) {
                return;
            }

            if (!equipoMap[row.equipo]) {
                equipoMap[row.equipo] = { enRuta: false, finalizado: false };
            }

            if (row.estado === "EN RUTA") {
                equipoMap[row.equipo].enRuta = true;
            } else if (row.estado === "FINALIZADO") {
                equipoMap[row.equipo].finalizado = true;
            }
        });

        Object.keys(equipoMap).forEach(function (equipo) {
            var info = equipoMap[equipo];
            if (info.enRuta && !info.finalizado) {
                equiposEnRutaSinCierre.push(equipo);
            }
        });

        if (equiposEnRutaSinCierre.length > 0) {
            alerts.push({
                level: "medium",
                text: "Equipos sin cierre finalizado en el periodo: " + equiposEnRutaSinCierre.slice(0, 3).join(", ") + ".",
                action: "Accion: confirmar estado real y registrar descarga para mantener trazabilidad."
            });
        }

        if (metrics.tritonCumpl < 90) {
            alerts.push({
                level: "medium",
                text: "TRITON por debajo del 90% de cumplimiento en el periodo (" + nf.format(metrics.tritonCumpl) + "%).",
                action: "Accion: redistribuir equipos hacia rutas TRITON en el siguiente turno."
            });
        }

        var incompletos = rows.filter(function (r) { return r.incompleto; }).length;
        var incompletosPct = rows.length > 0 ? (incompletos / rows.length) * 100 : 0;
        if (incompletosPct >= 5) {
            alerts.push({
                level: "medium",
                text: "Calidad de datos en riesgo: " + nf.format(incompletosPct) + "% de registros incompletos.",
                action: "Accion: completar conductor, ruta, equipo o estado antes del proximo cierre."
            });
        }

        if (!alerts.length) {
            alerts.push({
                level: "low",
                text: "Operacion estable: sin hallazgos criticos para el periodo filtrado.",
                action: "Accion: mantener ritmo operativo y monitoreo normal."
            });
        }

        var classByLevel = {
            high: "alert-tag alert-tag--high",
            medium: "alert-tag alert-tag--medium",
            low: "alert-tag alert-tag--low"
        };

        alertsList.innerHTML = alerts.slice(0, 6).map(function (alert) {
            var levelLabel = alert.level === "high" ? "ALTA" : (alert.level === "medium" ? "MEDIA" : "BAJA");
            return [
                "<li>",
                '<span class="' + classByLevel[alert.level] + '">' + levelLabel + "</span>",
                '<span class="small">' + alert.text + "</span>",
                '<div class="small text-muted mt-1">' + alert.action + "</div>",
                "</li>"
            ].join("");
        }).join("");
    }

    function renderQuality() {
        var rowsWithDate = rows.filter(function (r) { return !!r.fecha; });
        var latestDate = rowsWithDate.length
            ? startOfDay(new Date(Math.max.apply(null, rowsWithDate.map(function (r) { return r.fecha.getTime(); }))))
            : cutoffBase;
        var total = rows.length;
        var incompletos = rows.filter(function (r) { return r.incompleto; }).length;
        var completos = Math.max(0, total - incompletos);
        var completitud = total > 0 ? (completos / total) * 100 : 100;
        var sinFecha = rows.filter(function (r) { return !r.fecha; }).length;

        setText("qualityFecha", dateLabel.format(latestDate));
        setText("qualityTotalRegistros", ni.format(total));
        setText("qualityIncompletos", ni.format(incompletos));
        setText("qualityCompletitud", nf.format(completitud) + "%");
        setText("qualityFuente", "Fuente: " + (seed.sourceFileName || "Datos operativos") + " | Registros sin fecha: " + ni.format(sinFecha));
    }

    function renderComparatives(anchorDate) {
        var hoy = totalsForDay(anchorDate);
        var ayer = totalsForDay(addDays(anchorDate, -1));
        var promedio7 = averageLast7(anchorDate);

        var deltaVsAyerTotal = hoy.total - ayer.total;
        var deltaVsAyerTriton = hoy.triton - ayer.triton;

        var deltaVsPromTotal = hoy.total - promedio7.total;
        var deltaVsPromTriton = hoy.triton - promedio7.triton;

        var pctVsAyerTotal = ayer.total > 0 ? (deltaVsAyerTotal / ayer.total) * 100 : 0;
        var pctVsPromTotal = promedio7.total > 0 ? (deltaVsPromTotal / promedio7.total) * 100 : 0;

        setText("cmpTotalVsAyer", "Total: " + formatSigned(deltaVsAyerTotal, " Tn") + " (" + formatSigned(pctVsAyerTotal, "%") + ")");
        setText("cmpTritonVsAyer", "TRITON: " + formatSigned(deltaVsAyerTriton, " Tn"));
        setText("cmpTotalVsProm7", "Total: " + formatSigned(deltaVsPromTotal, " Tn") + " (" + formatSigned(pctVsPromTotal, "%") + ")");
        setText("cmpTritonVsProm7", "TRITON: " + formatSigned(deltaVsPromTriton, " Tn"));

        setText("kpiTritonComparativo", "Hoy vs ayer: " + formatSigned(deltaVsAyerTriton, " Tn") + " | vs promedio 7 dias: " + formatSigned(deltaVsPromTriton, " Tn"));
        setText("kpiTotalComparativo", "Hoy vs ayer: " + formatSigned(deltaVsAyerTotal, " Tn") + " | vs promedio 7 dias: " + formatSigned(deltaVsPromTotal, " Tn"));
    }

    function renderRange(rangeKey) {
        var bounds = getBounds(rangeKey);
        var rangeRows = rowsInBounds(bounds);
        var metrics = aggregateRange(rangeRows, bounds);
        var status = statusInfo(metrics.cumplimiento);
        currentRange = bounds.key;

        setText("execRangeLabel", bounds.label);
        setText("execFechaCorte", dateLabel.format(bounds.to));
        setText("execToneladasPeriodo", nf.format(metrics.totalTon) + " Tn");
        setText("execToneladasDetalle", "TRITON " + nf.format(metrics.tritonTon));
        setText("execCumplimiento", nf.format(metrics.cumplimiento) + "%");
        setText("execMetaDetalle", "Meta periodo " + nf.format(metrics.metaTotal) + " Tn");
        setText("execBrecha", nf.format(Math.abs(metrics.brecha)) + " Tn");
        setText("execBrechaDetalle", metrics.brecha > 0 ? "Pendiente por cubrir" : "Meta cubierta o superada");
        setText("execRegistrosPeriodo", ni.format(metrics.registros));
        setText("execEstadosDetalle", "Finalizados " + ni.format(metrics.finalizadosCount) + " | En ruta " + ni.format(metrics.enRutaCount));
        setText("execRangoDetalle", "Del " + dateLabel.format(bounds.from) + " al " + dateLabel.format(bounds.to));

        var statusEl = document.getElementById("execEstadoGeneral");
        if (statusEl) {
            statusEl.className = status.css;
            statusEl.textContent = status.text;
        }

        renderComparatives(bounds.to);
        renderTrend(bounds.to);
        renderMonthlyProgress(bounds.to);
        renderTopBottom(rangeRows);
        renderAlerts(metrics, rangeRows);
    }

    function setActiveRangeButton(rangeKey) {
        root.querySelectorAll(".quick-filter-btn").forEach(function (item) {
            item.classList.toggle("is-active", (item.getAttribute("data-range") || "") === rangeKey);
        });
    }

    function applyCustomRange() {
        var customBounds = resolveCustomBoundsFromInputs();
        if (!customBounds) {
            window.alert("Selecciona al menos una fecha en Desde/Hasta para aplicar el rango.");
            return;
        }

        currentCustomBounds = customBounds;
        setActiveRangeButton("custom");
        renderRange("custom");
        syncPdfDownloadLink();
    }

    function syncPdfDownloadLink() {
        var downloadLink = document.getElementById("descargar-seguimiento-pdf");
        if (!downloadLink) {
            return;
        }

        var base = downloadLink.getAttribute("href") || "";
        if (!base) {
            return;
        }

        var url = new URL(base, window.location.origin);
        url.searchParams.set("range", currentRange);
        if (currentRange === "custom" && currentCustomBounds) {
            url.searchParams.set("from", formatDateInput(currentCustomBounds.from));
            url.searchParams.set("to", formatDateInput(currentCustomBounds.to));
        } else {
            url.searchParams.delete("from");
            url.searchParams.delete("to");
        }

        downloadLink.setAttribute("href", url.pathname + url.search);
    }

    renderQuality();
    setActiveRangeButton("hoy");
    renderRange("hoy");
    syncPdfDownloadLink();

    root.querySelectorAll(".quick-filter-btn").forEach(function (btn) {
        btn.addEventListener("click", function () {
            var selectedRange = btn.getAttribute("data-range") || "hoy";
            if (selectedRange === "custom") {
                applyCustomRange();
                return;
            }

            setActiveRangeButton(selectedRange);
            renderRange(selectedRange);
            syncPdfDownloadLink();
        });
    });

    if (applyCustomRangeBtn) {
        applyCustomRangeBtn.addEventListener("click", function () {
            applyCustomRange();
        });
    }

    if (resetCustomRangeBtn) {
        resetCustomRangeBtn.addEventListener("click", function () {
            currentCustomBounds = null;
            if (customRangeFromInput) {
                customRangeFromInput.value = "";
            }

            if (customRangeToInput) {
                customRangeToInput.value = "";
            }

            setActiveRangeButton("hoy");
            renderRange("hoy");
            syncPdfDownloadLink();
        });
    }
});

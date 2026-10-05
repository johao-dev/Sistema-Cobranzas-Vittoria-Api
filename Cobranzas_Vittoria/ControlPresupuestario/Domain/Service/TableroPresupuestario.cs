namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Service;

/// <summary>Cabecera de entrada del tablero: el centro de costo y los presupuestos aprobados que lo componen.</summary>
public sealed record EncabezadoTablero(int IdCentroCosto, string CodigoCentroCosto, string NombreCentroCosto,
    string? NombreProyecto, int? IdMoneda, string? CodigoMoneda, string? SimboloMoneda, int CantidadMonedas,
    int CantidadPresupuestos, DateTime? FechaInicio, DateTime? FechaFin);

/// <summary>Montos vigentes de una partida, sumados sobre los presupuestos del tablero.</summary>
public sealed record RubroTablero(int IdCatalogoPartida, string CodigoPartida, string NombrePartida,
    decimal MontoPresupuestado, decimal MontoComprometido, decimal MontoEjecutado);

/// <summary>Efecto neto sobre lo ejecutado de una partida en un día.</summary>
public sealed record EjecucionTablero(DateTime Fecha, int IdCatalogoPartida, decimal MontoEjecutado);

public sealed record TableroEncabezado(int IdCentroCosto, string CodigoCentroCosto, string NombreCentroCosto,
    string? NombreProyecto, string? CodigoMoneda, string? SimboloMoneda, int CantidadPresupuestos,
    DateTime FechaInicio, DateTime FechaFin, DateTime FechaCorte, bool TieneCronograma)
{
    /// <summary>Nivel de anidamiento con que se agruparon los rubros; null = partidas finales.</summary>
    public int? Nivel { get; init; }

    /// <summary>Nivel más profundo de las partidas del tablero: rango del selector de nivel (1..NivelMaximo).</summary>
    public int NivelMaximo { get; init; }

    /// <summary>Rama a la que se limitó el tablero y su camino desde la raíz (para el breadcrumb); vacío = todo.</summary>
    public IReadOnlyList<TableroRama> Rama { get; init; } = Array.Empty<TableroRama>();
}

public sealed record TableroRama(int IdCatalogoPartida, string Codigo, string Nombre, int Nivel);

public sealed record TableroResumen(decimal Presupuestado, decimal Comprometido, decimal Ejecutado, decimal Desviacion,
    decimal? DesviacionPorcentaje, decimal? PorcentajeEjecutado);

public sealed record TableroRubro(int IdCatalogoPartida, string Codigo, string Nombre, decimal Presupuestado,
    decimal Comprometido, decimal Ejecutado, decimal PorcentajeDelTotal);

/// <summary>Semana puede ser fraccionaria: el punto de la fecha de corte cae entre dos semanas.</summary>
public sealed record TableroPuntoSemana(decimal Semana, DateTime Fecha, decimal? RealAcumulado, decimal? PresupuestoAcumulado);

public sealed record TableroDesviacionRubro(string Codigo, string Nombre, decimal PresupuestoAcumulado,
    decimal RealAcumulado, decimal Desviacion, decimal? DesviacionPorcentaje);

public sealed record TableroAlCorte(decimal? PresupuestoAcumulado, decimal RealAcumulado, decimal? Desviacion,
    decimal? DesviacionPorcentaje);

public sealed record Tablero(TableroEncabezado Encabezado, TableroResumen Resumen, IReadOnlyList<TableroRubro> Rubros,
    IReadOnlyList<TableroPuntoSemana> Semanas, TableroAlCorte AlCorte, IReadOnlyList<TableroDesviacionRubro> DesviacionPorRubro);

/// <summary>
/// Arma el tablero de un centro de costo: distribución del gasto por rubro y evolución semanal
/// del gasto real acumulado frente al presupuesto acumulado.
///
/// El sistema no guarda un cronograma de gasto, así que el presupuesto acumulado es lineal: el
/// total presupuestado se reparte parejo entre la fecha de inicio y la de fin del presupuesto.
/// Sin esas fechas no hay curva presupuestada ni desviación a la fecha, solo la curva real.
///
/// Fecha de corte: hoy, o la fecha del último gasto ejecutado si es posterior (un gasto puede
/// llevar la fecha futura de su documento, por ejemplo un alquiler adelantado). Así el
/// acumulado real siempre coincide con el ejecutado total.
/// </summary>
public static class TableroPresupuestario
{
    public static Tablero Construir(EncabezadoTablero encabezado, IReadOnlyList<RubroTablero> rubros,
        IReadOnlyList<EjecucionTablero> ejecuciones, DateTime hoy)
    {
        var ultimoGasto = ejecuciones.Count > 0 ? ejecuciones.Max(e => e.Fecha.Date) : hoy.Date;
        var corte = DateTime.SpecifyKind(Max(hoy.Date, ultimoGasto), DateTimeKind.Unspecified);
        var presupuestado = rubros.Sum(r => r.MontoPresupuestado);
        var ejecutado = rubros.Sum(r => r.MontoEjecutado);
        var comprometido = rubros.Sum(r => r.MontoComprometido);

        var distribucion = rubros
            .OrderByDescending(r => r.MontoEjecutado).ThenBy(r => r.CodigoPartida, StringComparer.Ordinal)
            .Select(r => new TableroRubro(r.IdCatalogoPartida, r.CodigoPartida, r.NombrePartida,
                r.MontoPresupuestado, r.MontoComprometido, r.MontoEjecutado,
                Porcentaje(r.MontoEjecutado, ejecutado) ?? 0m))
            .ToList();

        // Rango del gráfico: del inicio del presupuesto al fin (o hoy). La curva real siempre parte de 0:
        // si no hay fecha de inicio, o hay gastos anteriores a ella, el rango arranca una semana antes
        // del primer gasto. La curva presupuestada se calcula siempre sobre las fechas del presupuesto.
        var primerGasto = ejecuciones.Count > 0 ? ejecuciones.Min(e => e.Fecha.Date) : corte;
        var inicioPlan = encabezado.FechaInicio?.Date;
        var finPlan = encabezado.FechaFin?.Date;
        var tieneCronograma = inicioPlan is not null && finPlan is not null && finPlan > inicioPlan;
        var inicio = inicioPlan is { } ip && (ejecuciones.Count == 0 || ip < primerGasto) ? ip : primerGasto.AddDays(-7);
        var fin = Max(finPlan ?? corte, corte);
        if (fin <= inicio) fin = inicio.AddDays(7);

        decimal? Fraccion(DateTime fecha) => !tieneCronograma ? null
            : Math.Clamp((decimal)(fecha - inicioPlan!.Value).TotalDays / (decimal)(finPlan!.Value - inicioPlan.Value).TotalDays, 0m, 1m);

        var porFecha = ejecuciones.OrderBy(e => e.Fecha).ToList();
        decimal RealHasta(DateTime fecha, int? partida = null) => porFecha
            .Where(e => e.Fecha.Date <= fecha && (partida is null || e.IdCatalogoPartida == partida))
            .Sum(e => e.MontoEjecutado);

        decimal Semana(DateTime fecha) => decimal.Round((decimal)(fecha - inicio).TotalDays / 7m, 2);

        var semanas = new List<TableroPuntoSemana>();
        var totalSemanas = (int)Math.Ceiling((fin - inicio).TotalDays / 7d);
        for (var k = 0; k <= totalSemanas; k++)
        {
            var fecha = Min(inicio.AddDays(7 * k), fin);
            var plan = Fraccion(fecha) is { } f ? decimal.Round(presupuestado * f, 2) : (decimal?)null;
            // El gasto real se dibuja solo hasta la fecha de corte.
            decimal? real = fecha <= corte ? RealHasta(fecha) : null;
            semanas.Add(new TableroPuntoSemana(Semana(fecha), fecha, real, plan));
        }
        // Si hoy cae entre dos semanas, el último punto real es el de hoy.
        if (corte > inicio && corte < fin && semanas.All(s => s.Fecha != corte))
        {
            var planCorte = Fraccion(corte) is { } fc ? decimal.Round(presupuestado * fc, 2) : (decimal?)null;
            semanas.Add(new TableroPuntoSemana(Semana(corte), corte, RealHasta(corte), planCorte));
            semanas.Sort((a, b) => a.Fecha.CompareTo(b.Fecha));
        }

        var fraccionCorte = Fraccion(corte);
        var realCorte = RealHasta(corte);
        var planAlCorte = fraccionCorte is { } fr ? decimal.Round(presupuestado * fr, 2) : (decimal?)null;
        var alCorte = new TableroAlCorte(planAlCorte, realCorte,
            planAlCorte is null ? null : realCorte - planAlCorte,
            planAlCorte is null ? null : Porcentaje(realCorte - planAlCorte.Value, planAlCorte.Value));

        var desviaciones = fraccionCorte is not { } fcorte ? new List<TableroDesviacionRubro>()
            : rubros
                .Select(r =>
                {
                    var plan = decimal.Round(r.MontoPresupuestado * fcorte, 2);
                    var real = RealHasta(corte, r.IdCatalogoPartida);
                    return new TableroDesviacionRubro(r.CodigoPartida, r.NombrePartida, plan, real,
                        real - plan, Porcentaje(real - plan, plan));
                })
                .Where(d => d.PresupuestoAcumulado != 0 || d.RealAcumulado != 0)
                .OrderByDescending(d => d.Desviacion)
                .ToList();

        return new Tablero(
            new TableroEncabezado(encabezado.IdCentroCosto, encabezado.CodigoCentroCosto, encabezado.NombreCentroCosto,
                encabezado.NombreProyecto, encabezado.CodigoMoneda, encabezado.SimboloMoneda,
                encabezado.CantidadPresupuestos, inicio, fin, corte, tieneCronograma),
            new TableroResumen(presupuestado, comprometido, ejecutado, ejecutado - presupuestado,
                Porcentaje(ejecutado - presupuestado, presupuestado), Porcentaje(ejecutado, presupuestado)),
            distribucion, semanas, alCorte, desviaciones);
    }

    private static decimal? Porcentaje(decimal parte, decimal total)
        => total == 0 ? null : decimal.Round(parte * 100m / total, 2, MidpointRounding.AwayFromZero);

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}

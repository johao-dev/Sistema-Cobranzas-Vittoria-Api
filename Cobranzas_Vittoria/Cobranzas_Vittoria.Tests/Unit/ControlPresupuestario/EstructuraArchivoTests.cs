using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.ImportarEstructura;

namespace Cobranzas_Vittoria.Tests.Unit.ControlPresupuestario;

/// <summary>Lectura del presupuesto jerárquico: código por columna o por niveles, categorías sin monto y hojas con monto.</summary>
public class EstructuraArchivoTests
{
    private static IReadOnlyList<FilaTabular> Archivo(string[] encabezados, params string?[][] filas)
        => filas.Select((valores, i) => new FilaTabular(i + 1,
            encabezados.Select((e, j) => (e, valores[j])).ToDictionary(x => x.e, x => x.Item2))).ToList();

    [Test]
    public void Niveles_ArmanElCodigoYDistinguenCategoriasDeHojas()
    {
        var (filas, errores) = EstructuraArchivo.Leer(Archivo(
            new[] { "nivel 1", "Nivel 2", "NIVEL 3", "Descripción", "Und.", "Total" },
            new[] { "1", "0", "0", "GASTOS DIRECTOS", "", "" },
            new[] { "1", "1", "0", "  ESTRUCTURAS ", "", "0" },
            new[] { "1", "1", "1", "DEMOLICION", "glb", "3816.4349999999999" },
            new[] { "1", "1", "10", "TRAZO", "m2", "0" },
            new[] { "", "", "", "", "", "" }));

        Assert.That(errores, Is.Empty, string.Join("; ", errores.Select(e => e.Mensaje)));
        Assert.That(filas.Select(f => f.Codigo), Is.EqualTo(new[] { "1", "1.1", "1.1.1", "1.1.10" }), "La fila vacía se ignora.");
        Assert.That(filas.Select(f => f.EsCategoria), Is.EqualTo(new[] { true, true, false, false }));
        Assert.That(filas[2].CodigoPadre, Is.EqualTo("1.1"));
        Assert.That(filas[1].Nombre, Is.EqualTo("ESTRUCTURAS"));
        Assert.That(filas[2].Monto, Is.EqualTo(3816.43m), "Los montos con decimales de fórmula se redondean al céntimo.");
        Assert.That(filas[3].Monto, Is.Zero);
    }

    [Test]
    public void ColumnaCodigo_SeAceptaConNombreYMonto()
    {
        var (filas, errores) = EstructuraArchivo.Leer(Archivo(new[] { "Codigo", "Nombre", "Monto", "Tipo", "Seccion", "Observacion" },
            new[] { "01", "Terreno", null, "INDIRECTOS", null, null },
            new[] { "01.01", "Compra", "S/ 1,250.50", null, "TERRENO", "Escritura" }));

        Assert.That(errores, Is.Empty);
        Assert.That(filas[1].Monto, Is.EqualTo(1250.50m));
        Assert.That((filas[0].Tipo, filas[1].Seccion, filas[1].Observacion), Is.EqualTo(("INDIRECTOS", "TERRENO", "Escritura")));
    }

    [Test]
    public void CategoriaConMonto_CodigoRepetidoYNivelesSaltados_SonErrores()
    {
        var (_, errores) = EstructuraArchivo.Leer(Archivo(new[] { "Nivel 1", "Nivel 2", "Nivel 3", "Descripcion", "Total" },
            new[] { "2", "0", "0", "INDIRECTOS", "500" },
            new[] { "2", "2", "0", "TRAMITES", "0" },
            new[] { "2", "2", "0", "NOTARIALES", "100" },
            new[] { "2", "0", "3", "SALTADO", "1" },
            new[] { "2", "1", "0", "", "1" }));
        var codigos = errores.Select(e => (e.Fila, e.CodigoError)).ToList();

        Assert.That(codigos, Does.Contain((1, EstructuraArchivo.PadreConMonto)));
        Assert.That(codigos, Does.Contain((3, "VALOR_DUPLICADO_EN_ARCHIVO")));
        Assert.That(codigos, Does.Contain((4, EstructuraArchivo.NivelesInvalidos)));
        Assert.That(codigos, Does.Contain((5, "CAMPO_REQUERIDO")));
        Assert.That(codigos, Does.Not.Contain((2, EstructuraArchivo.PadreConMonto)), "Una categoría puede traer 0.");
    }

    [Test]
    public void SinColumnasRequeridas_Devuelve400ConLosEncabezadosRecibidos()
    {
        var ex = Assert.Throws<EstructuraInvalidaException>(() => EstructuraArchivo.Leer(Archivo(new[] { "Descripcion", "Precio" },
            new[] { "X", "1" })));
        Assert.That(ex!.Message, Does.Contain("Codigo (o Nivel 1").And.Contain("Monto (o Total)").And.Contain("Precio"));
    }

    [TestCase("1.1.3", "1.1")]
    [TestCase("01", null)]
    public void Padre_EsElCodigoSinSuUltimoSegmento(string codigo, string? padre)
        => Assert.That(EstructuraArchivo.Padre(codigo), Is.EqualTo(padre));

    [Test]
    public void MismoNombre_IgnoraMayusculasTildesYEspacios()
        => Assert.That(EstructuraArchivo.MismoNombre("Demolición  de obra", "DEMOLICION DE OBRA"), Is.True);
}

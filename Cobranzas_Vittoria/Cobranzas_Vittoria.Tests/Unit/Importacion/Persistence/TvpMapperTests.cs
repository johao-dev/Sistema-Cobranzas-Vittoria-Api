using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Persistence;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Persistence;

/// <summary>
/// Pruebas unitarias de <see cref="TvpMapper"/>.
/// Verifica que la conversion DTO -&gt; DataTable produzca columnas con los tipos
/// correctos y los valores mapeados fila por fila.
/// </summary>
public class TvpMapperTests
{
    [Test]
    public void ToDataTable_ConUnidadMedidaDto_DevuelveColumnasEsperadas()
    {
        var dataTable = TvpMapper.ToDataTable(Array.Empty<UnidadMedidaImportDto>());

        Assert.That(dataTable.Columns.Count, Is.EqualTo(4));
        Assert.That(dataTable.Columns.Contains("_Fila"), Is.True);
        Assert.That(dataTable.Columns.Contains("Codigo"), Is.True);
        Assert.That(dataTable.Columns.Contains("Nombre"), Is.True);
        Assert.That(dataTable.Columns.Contains("Activo"), Is.True);
    }

    [Test]
    public void ToDataTable_ColumnasTienenTipoNetCorrecto()
    {
        var dataTable = TvpMapper.ToDataTable(Array.Empty<UnidadMedidaImportDto>());

        Assert.That(dataTable.Columns["_Fila"]!.DataType, Is.EqualTo(typeof(int)));
        Assert.That(dataTable.Columns["Codigo"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Nombre"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Activo"]!.DataType, Is.EqualTo(typeof(bool)));
    }

    [Test]
    public void ToDataTable_ListaVacia_DevuelveDataTableConColumnasSinFilas()
    {
        var dataTable = TvpMapper.ToDataTable(Array.Empty<UnidadMedidaImportDto>());

        Assert.That(dataTable.Rows.Count, Is.EqualTo(0));
        Assert.That(dataTable.Columns.Count, Is.EqualTo(4));
    }

    [Test]
    public void ToDataTable_ConDtos_MapeaValoresCorrectamente()
    {
        var dtos = new[]
        {
            new UnidadMedidaImportDto { _Fila = 2, Codigo = "UM-001", Nombre = "Kilogramo", Activo = true },
            new UnidadMedidaImportDto { _Fila = 3, Codigo = "UM-002", Nombre = "Metro", Activo = false }
        };

        var dataTable = TvpMapper.ToDataTable(dtos);

        Assert.That(dataTable.Rows.Count, Is.EqualTo(2));

        Assert.That(dataTable.Rows[0]["_Fila"], Is.EqualTo(2));
        Assert.That(dataTable.Rows[0]["Codigo"], Is.EqualTo("UM-001"));
        Assert.That(dataTable.Rows[0]["Nombre"], Is.EqualTo("Kilogramo"));
        Assert.That(dataTable.Rows[0]["Activo"], Is.EqualTo(true));

        Assert.That(dataTable.Rows[1]["_Fila"], Is.EqualTo(3));
        Assert.That(dataTable.Rows[1]["Codigo"], Is.EqualTo("UM-002"));
        Assert.That(dataTable.Rows[1]["Activo"], Is.EqualTo(false));
    }

    [Test]
    public void ToDataTable_NombreEsNull_SeConvierteADbNull()
    {
        var dtos = new[]
        {
            new UnidadMedidaImportDto { _Fila = 1, Codigo = "UM-001", Nombre = null!, Activo = true }
        };

        var dataTable = TvpMapper.ToDataTable(dtos);

        Assert.That(dataTable.Rows[0]["Nombre"], Is.EqualTo(DBNull.Value));
    }

    [Test]
    public void ToDataTable_PropiedadIntNullable_SeMapeaComoInt()
    {
        // El mapper usa Nullable.GetUnderlyingType para que la columna del DataTable
        // sea del tipo subyacente (int, no int?).
        var dataTable = TvpMapper.ToDataTable(Array.Empty<DtoConIntNullable>());

        Assert.That(dataTable.Columns["Cantidad"]!.DataType, Is.EqualTo(typeof(int)));
    }

    [Test]
    public void ToDataTable_DtosConNulosEnColeccion_LosSaltea()
    {
        var dtos = new UnidadMedidaImportDto?[]
        {
            new() { _Fila = 1, Codigo = "UM-001", Nombre = "A", Activo = true },
            null,
            new() { _Fila = 3, Codigo = "UM-002", Nombre = "B", Activo = true }
        };

        var dataTable = TvpMapper.ToDataTable(dtos!);

        Assert.That(dataTable.Rows.Count, Is.EqualTo(2));
    }

    [Test]
    public void ToDataTable_LlamaDosVecesMismoTipo_UsaCacheDePropiedades()
    {
        // No hay assertion observable del cache, pero verificamos que
        // dos llamadas sucesivas no fallen (smoke test del ConcurrentDictionary interno).
        var dtos1 = new[] { new UnidadMedidaImportDto { _Fila = 1, Codigo = "X", Nombre = "Y", Activo = true } };
        var dtos2 = new[] { new UnidadMedidaImportDto { _Fila = 2, Codigo = "A", Nombre = "B", Activo = false } };

        var t1 = TvpMapper.ToDataTable(dtos1);
        var t2 = TvpMapper.ToDataTable(dtos2);

        Assert.That(t1.Rows.Count, Is.EqualTo(1));
        Assert.That(t2.Rows.Count, Is.EqualTo(1));
        Assert.That(t1.Columns.Count, Is.EqualTo(4));
        Assert.That(t2.Columns.Count, Is.EqualTo(4));
    }

    // ====================================================================
    // Cobertura de los 6 DTOs adicionales (Especialidad, Material, Proveedor,
    // ProveedorGastoAdministrativo, ProveedorTerreno, CategoriaGasto).
    // Verifica que la cantidad de columnas y sus tipos coincidan con el TVP
    // correspondiente. El orden de columnas es importante porque TvpMapper usa
    // el orden de declaracion de las propiedades, y el SQL rechaza la insercion
    // si los tipos no calzan (ej. string -> bit).
    // ====================================================================

    [Test]
    public void ToDataTable_ConEspecialidadDto_DevuelveColumnasEsperadas()
    {
        var dataTable = TvpMapper.ToDataTable(Array.Empty<EspecialidadImportDto>());

        Assert.That(dataTable.Columns.Count, Is.EqualTo(4));
        Assert.That(dataTable.Columns["Nombre"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Descripcion"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Activo"]!.DataType, Is.EqualTo(typeof(bool)));
        Assert.That(dataTable.Columns["_Fila"]!.DataType, Is.EqualTo(typeof(int)));
    }

    [Test]
    public void ToDataTable_ConMaterialDto_DevuelveColumnasYTiposEsperados()
    {
        var dataTable = TvpMapper.ToDataTable(Array.Empty<MaterialImportDto>());

        Assert.That(dataTable.Columns.Count, Is.EqualTo(9));
        Assert.That(dataTable.Columns["IdEspecialidad"]!.DataType, Is.EqualTo(typeof(int)));
        Assert.That(dataTable.Columns["Codigo"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Descripcion"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["UnidadMedida"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["StockMinimo"]!.DataType, Is.EqualTo(typeof(decimal)));
        Assert.That(dataTable.Columns["Activo"]!.DataType, Is.EqualTo(typeof(bool)));
        Assert.That(dataTable.Columns["IdUnidadMedida"]!.DataType, Is.EqualTo(typeof(int)));
        Assert.That(dataTable.Columns["CodigoProveedor"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["_Fila"]!.DataType, Is.EqualTo(typeof(int)));
    }

    [Test]
    public void ToDataTable_ConProveedorDto_DevuelveColumnasYTiposEsperados()
    {
        var dataTable = TvpMapper.ToDataTable(Array.Empty<ProveedorImportDto>());

        Assert.That(dataTable.Columns.Count, Is.EqualTo(15));
        Assert.That(dataTable.Columns["RazonSocial"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Ruc"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Contacto"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Telefono"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Correo"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Direccion"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Banco"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["CuentaCorriente"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["CCI"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["CuentaDetraccion"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["DescripcionServicio"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Observacion"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["TrabajamosConProveedor"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Activo"]!.DataType, Is.EqualTo(typeof(bool)));
        Assert.That(dataTable.Columns["_Fila"]!.DataType, Is.EqualTo(typeof(int)));
    }

    [Test]
    public void ToDataTable_ConProveedorGastoAdministrativoDto_DevuelveColumnasYTiposEsperados()
    {
        var dataTable = TvpMapper.ToDataTable(Array.Empty<ProveedorGastoAdministrativoImportDto>());

        Assert.That(dataTable.Columns.Count, Is.EqualTo(8));
        Assert.That(dataTable.Columns["RazonSocial"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Ruc"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Contacto"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Telefono"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Correo"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Activo"]!.DataType, Is.EqualTo(typeof(bool)));
        Assert.That(dataTable.Columns["IdCategoriaGasto"]!.DataType, Is.EqualTo(typeof(int)));
        Assert.That(dataTable.Columns["_Fila"]!.DataType, Is.EqualTo(typeof(int)));
    }

    [Test]
    public void ToDataTable_ConProveedorTerrenoDto_DevuelveColumnasYTiposEsperados()
    {
        var dataTable = TvpMapper.ToDataTable(Array.Empty<ProveedorTerrenoImportDto>());

        Assert.That(dataTable.Columns.Count, Is.EqualTo(7));
        Assert.That(dataTable.Columns["RazonSocial"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Ruc"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Contacto"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Telefono"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Correo"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Activo"]!.DataType, Is.EqualTo(typeof(bool)));
        Assert.That(dataTable.Columns["_Fila"]!.DataType, Is.EqualTo(typeof(int)));
    }

    [Test]
    public void ToDataTable_ConCategoriaGastoDto_DevuelveColumnasEsperadas()
    {
        var dataTable = TvpMapper.ToDataTable(Array.Empty<CategoriaGastoImportDto>());

        Assert.That(dataTable.Columns.Count, Is.EqualTo(3));
        Assert.That(dataTable.Columns["Nombre"]!.DataType, Is.EqualTo(typeof(string)));
        Assert.That(dataTable.Columns["Activo"]!.DataType, Is.EqualTo(typeof(bool)));
        Assert.That(dataTable.Columns["_Fila"]!.DataType, Is.EqualTo(typeof(int)));
    }

    [Test]
    public void ToDataTable_ConMaterialDto_MapeaValoresIncluyendoDecimalesYNulables()
    {
        var dtos = new[]
        {
            new MaterialImportDto
            {
                _Fila = 2,
                IdEspecialidad = 1,
                Codigo = "MAT-TEST-1",
                Descripcion = "Cemento",
                UnidadMedida = "BOL",
                StockMinimo = 100.50m,
                Activo = true,
                IdUnidadMedida = null,         // opcional
                CodigoProveedor = "ABC-123"
            }
        };

        var dataTable = TvpMapper.ToDataTable(dtos);

        Assert.That(dataTable.Rows.Count, Is.EqualTo(1));
        Assert.That(dataTable.Rows[0]["IdEspecialidad"], Is.EqualTo(1));
        Assert.That(dataTable.Rows[0]["Codigo"], Is.EqualTo("MAT-TEST-1"));
        Assert.That(dataTable.Rows[0]["StockMinimo"], Is.EqualTo(100.50m));
        Assert.That(dataTable.Rows[0]["IdUnidadMedida"], Is.EqualTo(DBNull.Value));
        Assert.That(dataTable.Rows[0]["CodigoProveedor"], Is.EqualTo("ABC-123"));
    }

    private sealed class DtoConIntNullable
    {
        public int? Cantidad { get; set; }
    }
}

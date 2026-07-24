# Dependencias de la aplicación

Las versiones se administran centralmente en `Directory.Packages.props`. No se usan versiones preliminares. Las dependencias transitivas continúan sujetas a la restauración y auditoría de NuGet.

| Dependencia | Versión | Propósito | Licencia | Impacto | Proyecto |
|---|---:|---|---|---|---|
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.10 | Proveedor EF Core para SQLite | MIT | Persistencia local y dependencias transitivas de EF Core/SQLite | Infrastructure |
| Microsoft.EntityFrameworkCore.Design | 10.0.10 | Crear y mantener migraciones | MIT | Solo desarrollo; no se publica con la aplicación | Infrastructure |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.12 | Binario nativo de SQLite corregido | Apache-2.0 | Aumenta el tamaño de restauración/publicación; fija la versión transitiva vulnerable 2.1.11 | Infrastructure |
| CommunityToolkit.Mvvm | 8.4.2 | Propiedades observables y comandos MVVM | MIT | Generadores de código y biblioteca ligera en la interfaz | App |
| Microsoft.Extensions.DependencyInjection | 10.0.10 | Composición de dependencias al arrancar | MIT | Contenedor de servicios en tiempo de ejecución | App |
| Velopack | 1.2.0 | Instalación, consulta, descarga y aplicación de actualizaciones | MIT | Agrega el bootstrap y cliente de GitHub Releases al ejecutable | App |
| ClosedXML | 0.105.0 | Creación y lectura de libros `.xlsx` sin automatizar Microsoft Excel | MIT | Exportación completa con tablas, filtros y formatos de Excel | Infrastructure |
| OxyPlot.Wpf | 2.2.0 | Gráficos 2D del resumen mensual | MIT | Barras, composición circular y evolución mensual; no aparece en Inicio | App |
| Microsoft.NET.Test.Sdk | 18.8.1 | Descubrimiento y ejecución de pruebas .NET | MIT | Solo desarrollo y CI | Los tres proyectos Tests |
| xunit.v3 | 3.2.2 | Marco de pruebas | Apache-2.0 | Solo desarrollo y CI | Los tres proyectos Tests |
| xunit.runner.visualstudio | 3.1.5 | Adaptador de xUnit para `dotnet test` y Visual Studio | Apache-2.0 | Solo desarrollo y CI; activo privado | Los tres proyectos Tests |
| dotnet-ef | 10.0.10 | Comandos locales de migraciones EF Core | MIT | Herramienta local restaurable; no es instalación global | Repositorio (`dotnet-tools.json`) |

Todos los paquetes de Entity Framework Core y la herramienta `dotnet-ef` usan exactamente la versión 10.0.10. `SQLitePCLRaw.lib.e_sqlite3` 2.1.12 se referencia directamente para evitar la versión 2.1.11, retirada por problemas críticos y una vulnerabilidad conocida.

El CLI `vpk` debe ejecutarse siempre en la misma versión 1.2.0 que el paquete Velopack. El workflow lo instala en una carpeta efímera; no requiere una instalación global ni almacena tokens en el repositorio.

ClosedXML se usa únicamente para generar el libro local. No requiere Microsoft Excel instalado. Su versión está centralizada, igual que el resto de paquetes, y su licencia MIT es compatible con la licencia del repositorio.

OxyPlot.Wpf se usa solo para gráficos 2D accesibles del Resumen mensual. La versión se fija centralmente y su licencia MIT es compatible con el repositorio.

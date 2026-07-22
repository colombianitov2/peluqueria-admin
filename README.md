# Peluquería Admin

> Estado: primera versión alfa administrativa completa, pendiente de revisión del PR y publicación deliberada.

Peluquería Admin será una aplicación local para administrar el funcionamiento interno de una peluquería. Su propósito es registrar ingresos propios del local, gastos, obligaciones, inventario y el punto de equilibrio mensual.

No es un sistema contable ni fiscal oficial. No calcula declaraciones, no implementa normativa fiscal específica y no presenta información ante entidades gubernamentales.

## Modelo general del negocio

Las personas que trabajan en el local utilizan sus propios implementos y cobran directamente sus servicios; esos cobros no son ingresos del local. El local registra, entre otros conceptos permitidos, los pagos por uso del espacio, las ventas propias, sus gastos, obligaciones e inventario.

## Arquitectura adoptada

- C# y .NET 10.
- WPF para la aplicación de escritorio Windows.
- SQLite para los datos locales.
- Velopack 1.2.0 para instalación y actualizaciones mediante GitHub Releases públicos.

## Estado de implementación

La aplicación implementa Inicio, Uso del local, Colaboradores, Ventas, Inventario, Otros ingresos, Gastos, Imprevistos, Obligaciones, Mantenimiento, Resumen mensual, Balance anual, Ajustes y un bloc único de Notas. Trabaja en USD, calcula resultados desde movimientos reales, distribuye el fondo global mediante participaciones internas, conserva snapshots mensuales automáticos, ofrece tablas con desplazamiento interno, copias/restauración y una exportación `.xlsx` completa sin planes de reposición.

El instalador alpha x64 se construyó localmente sin firma y permanece ignorado por Git. No se ha publicado un Release ni se ha probado todavía una actualización real entre dos Releases.

## Estructura de proyectos

- `PeluqueriaAdmin.Domain`: reglas y modelos del negocio, sin dependencias de infraestructura o interfaz.
- `PeluqueriaAdmin.Application`: casos de uso, contratos y coordinación; depende de Domain.
- `PeluqueriaAdmin.Infrastructure`: persistencia SQLite, migraciones, inicialización y rutas locales; depende de Application y Domain.
- `PeluqueriaAdmin.App`: interfaz WPF, composición y arranque; depende de Application e Infrastructure.
- `PeluqueriaAdmin.Domain.Tests`: pruebas de validaciones y representaciones exactas del dominio.
- `PeluqueriaAdmin.Infrastructure.Tests`: pruebas de migración y persistencia sobre bases temporales.
- `PeluqueriaAdmin.Application.Tests`: pruebas de los casos de uso críticos.

## Requisitos actuales para desarrollo

El desarrollo requiere Windows y un SDK compatible con el `global.json` del repositorio, actualmente .NET SDK 10.0.301 con avance permitido dentro de .NET 10.

Para restaurar y compilar desde la raíz:

```powershell
dotnet tool restore
dotnet restore PeluqueriaAdmin.sln
dotnet build PeluqueriaAdmin.sln --configuration Debug --no-restore
dotnet build PeluqueriaAdmin.sln --configuration Release --no-restore
dotnet test PeluqueriaAdmin.sln --configuration Release --no-build
```

Para verificar o crear migraciones desde la raíz:

```powershell
dotnet ef migrations list --project src/PeluqueriaAdmin.Infrastructure --startup-project src/PeluqueriaAdmin.Infrastructure
dotnet ef migrations add NombreDescriptivo --project src/PeluqueriaAdmin.Infrastructure --startup-project src/PeluqueriaAdmin.Infrastructure --output-dir Persistence/Migrations
```

Para publicar una compilación autocontenida x64 y crear un paquete local de desarrollo:

```powershell
dotnet publish src/PeluqueriaAdmin.App/PeluqueriaAdmin.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish -p:Version=0.1.0-alpha.1
dotnet tool install --tool-path artifacts/tools vpk --version 1.2.0
artifacts/tools/vpk pack --packId Colombianito.PeluqueriaAdmin --packVersion 0.1.0-alpha.1 --packDir artifacts/publish --mainExe PeluqueriaAdmin.App.exe --packTitle "Peluquería Admin" --runtime win-x64 --outputDir Releases
```

`artifacts/` y `Releases/` están ignorados. El workflow `release.yml` publica únicamente al empujar deliberadamente una etiqueta SemVer `v*`.

## Datos locales

La aplicación crea la base en `%LocalAppData%\PeluqueriaAdmin\Data\peluqueria-admin.db` y usa las carpetas `Backups`, `Exports` y `Logs` bajo la misma raíz. El ejecutable y los datos permanecen separados para conservarlos durante actualizaciones.

Las pruebas sustituyen esa ruta por carpetas temporales y nunca utilizan la base real del usuario.

## Protección de datos

No se deben subir datos reales de la peluquería, bases de datos, copias de seguridad, registros, secretos, certificados ni claves de firma. Las reglas iniciales de protección se encuentran en `.gitignore`.

## Documentación

- [Requisitos vigentes](docs/REQUISITOS_VIGENTES.md)
- [Arquitectura propuesta y adoptada](docs/ARQUITECTURA_PROPUESTA.md)
- [Decisiones pendientes](docs/DECISIONES_PENDIENTES.md)
- [Dependencias](docs/DEPENDENCIAS.md)
- [Modelo de datos](docs/MODELO_DATOS.md)
- [Fórmulas financieras](docs/FORMULAS_FINANCIERAS.md)
- [Copias y restauración](docs/COPIAS_Y_RESTAURACION.md)
- [Actualizaciones y Releases](docs/ACTUALIZACIONES_Y_RELEASES.md)
- [Manual de usuario](docs/MANUAL_USUARIO.md)
- [Pruebas](docs/PRUEBAS.md)

## Contribuciones

Se aceptan mejoras y contribuciones que respeten los requisitos vigentes y mantengan cambios pequeños, seguros y revisables. Consulta [CONTRIBUTING.md](CONTRIBUTING.md) antes de proponer cambios.

## Licencia

Este proyecto se distribuye bajo la [licencia MIT](LICENSE).

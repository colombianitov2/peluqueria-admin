# Peluquería Admin

> Estado: base técnica, persistencia SQLite y configuración general implementadas.

Peluquería Admin será una aplicación local para administrar el funcionamiento interno de una peluquería. Su propósito es registrar ingresos propios del local, gastos, obligaciones, inventario y el punto de equilibrio mensual.

No es un sistema contable ni fiscal oficial. No calcula declaraciones, no implementa normativa fiscal específica y no presenta información ante entidades gubernamentales.

## Modelo general del negocio

Las personas que trabajan en el local utilizan sus propios implementos y cobran directamente sus servicios; esos cobros no son ingresos del local. El local registra, entre otros conceptos permitidos, los pagos por uso del espacio, las ventas propias, sus gastos, obligaciones e inventario.

## Arquitectura adoptada

- C# y .NET 10.
- WPF para la aplicación de escritorio Windows.
- SQLite para los datos locales.
- Velopack, previsto para la instalación y actualizaciones mediante GitHub Releases públicos.

## Estado de implementación

Existe una solución compilable en .NET 10 con separación por capas, navegación entre Inicio y Ajustes, una configuración general persistida mediante SQLite y pruebas automatizadas. Todavía no existe instalador ni actualización automática. Tampoco se han implementado cobros semanales, inventario, ventas, colaboradores, fórmulas de punto de equilibrio ni reparto de ganancias.

## Estructura de proyectos

- `PeluqueriaAdmin.Domain`: reglas y modelos del negocio, sin dependencias de infraestructura o interfaz.
- `PeluqueriaAdmin.Application`: casos de uso, contratos y coordinación; depende de Domain.
- `PeluqueriaAdmin.Infrastructure`: persistencia SQLite, migraciones, inicialización y rutas locales; depende de Application y Domain.
- `PeluqueriaAdmin.App`: interfaz WPF, composición y arranque; depende de Application e Infrastructure.
- `PeluqueriaAdmin.Domain.Tests`: pruebas de validaciones y representaciones exactas del dominio.
- `PeluqueriaAdmin.Infrastructure.Tests`: pruebas de migración y persistencia sobre bases temporales.

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

Estos comandos son para desarrollo; todavía no generan un instalador ni configuran actualizaciones automáticas.

## Datos locales

La aplicación crea la base de datos en `%LocalAppData%\PeluqueriaAdmin\Data\peluqueria-admin.db`. También prepara `%LocalAppData%\PeluqueriaAdmin\Backups` y `%LocalAppData%\PeluqueriaAdmin\Logs` para fases futuras. El ejecutable y los datos se mantienen en carpetas distintas.

Las pruebas sustituyen esa ruta por carpetas temporales y nunca utilizan la base real del usuario.

## Protección de datos

No se deben subir datos reales de la peluquería, bases de datos, copias de seguridad, registros, secretos, certificados ni claves de firma. Las reglas iniciales de protección se encuentran en `.gitignore`.

## Documentación

- [Requisitos vigentes](docs/REQUISITOS_VIGENTES.md)
- [Arquitectura propuesta y adoptada](docs/ARQUITECTURA_PROPUESTA.md)
- [Decisiones pendientes](docs/DECISIONES_PENDIENTES.md)
- [Dependencias](docs/DEPENDENCIAS.md)

## Contribuciones

Se aceptan mejoras y contribuciones que respeten los requisitos vigentes y mantengan cambios pequeños, seguros y revisables. Consulta [CONTRIBUTING.md](CONTRIBUTING.md) antes de proponer cambios.

## Licencia

Este proyecto se distribuye bajo la [licencia MIT](LICENSE).

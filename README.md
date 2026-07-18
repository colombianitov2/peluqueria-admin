# Peluquería Admin

> Estado: solución técnica base creada; módulos funcionales todavía no implementados.

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

Existe una solución compilable en .NET 10 con una aplicación WPF mínima y separación por capas. Todavía no existen módulos funcionales, base de datos, instalador ni actualización automática.

## Estructura de proyectos

- `PeluqueriaAdmin.Domain`: reglas y modelos del negocio, sin dependencias de infraestructura o interfaz.
- `PeluqueriaAdmin.Application`: casos de uso, contratos y coordinación; depende de Domain.
- `PeluqueriaAdmin.Infrastructure`: futuras implementaciones de persistencia, copias, migraciones y actualizaciones; depende de Application y Domain.
- `PeluqueriaAdmin.App`: interfaz WPF, composición y arranque; depende de Application e Infrastructure.

## Requisitos actuales para desarrollo

El desarrollo requiere Windows y un SDK compatible con el `global.json` del repositorio, actualmente .NET SDK 10.0.301 con avance permitido dentro de .NET 10.

Para restaurar y compilar desde la raíz:

```powershell
dotnet restore PeluqueriaAdmin.sln
dotnet build PeluqueriaAdmin.sln --configuration Debug --no-restore
dotnet build PeluqueriaAdmin.sln --configuration Release --no-restore
```

Estos comandos compilan la solución para desarrollo; todavía no generan un instalador ni configuran actualizaciones automáticas.

## Protección de datos

No se deben subir datos reales de la peluquería, bases de datos, copias de seguridad, registros, secretos, certificados ni claves de firma. Las reglas iniciales de protección se encuentran en `.gitignore`.

## Documentación

- [Requisitos vigentes](docs/REQUISITOS_VIGENTES.md)
- [Arquitectura propuesta y adoptada](docs/ARQUITECTURA_PROPUESTA.md)
- [Decisiones pendientes](docs/DECISIONES_PENDIENTES.md)

## Contribuciones

Se aceptan mejoras y contribuciones que respeten los requisitos vigentes y mantengan cambios pequeños, seguros y revisables. Consulta [CONTRIBUTING.md](CONTRIBUTING.md) antes de proponer cambios.

## Licencia

Este proyecto se distribuye bajo la [licencia MIT](LICENSE).

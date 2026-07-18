# Peluquería Admin

> Estado: planificación y preparación inicial.

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

Todavía no existe la solución .NET ni funcionalidades implementadas. Este repositorio contiene la preparación documental y de publicación del proyecto.

## Requisitos actuales para desarrollo

El desarrollo requerirá Windows y el SDK de .NET 10. Las dependencias y el flujo de compilación se documentarán cuando exista una solución aprobada; por ahora no hay instrucciones de instalación o descarga de la aplicación.

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

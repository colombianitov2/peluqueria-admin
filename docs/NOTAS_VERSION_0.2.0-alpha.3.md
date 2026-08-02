# Peluquería Admin 0.2.0-alpha.3

Versión preliminar pública correctiva para instalaciones nuevas y actualizaciones desde `0.2.0-alpha.2`.

[Descargar el instalador público de Peluquería Admin 0.2.0-alpha.3](https://github.com/colombianitov2/peluqueria-admin/releases/download/v0.2.0-alpha.3/PeluqueriaAdmin-Setup-v0.2.0-alpha.3.exe)

## Correcciones críticas

- Normaliza los identificadores GUID creados por migraciones antiguas para que coincidan con el formato utilizado por Entity Framework y SQLite.
- Corrige el fallo de clave foránea que podía impedir que la aplicación abriera al generar cargos diarios.
- Conserva los datos, cargos, tarifas, sillas, asignaciones, aportes, préstamos e historiales existentes.
- Mantiene la política aprobada para el antiguo valor heredado de USD 12.
- Registra la excepción completa de cualquier fallo de inicio en la carpeta local `Logs`.
- Muestra en pantalla la causa raíz y la ubicación del archivo de diagnóstico.
- Corrige `ProductVersion`, `FileVersion`, `AssemblyVersion` e `InformationalVersion`.
- El workflow detiene la publicación si el ejecutable no coincide con la etiqueta.

## Cambios ya incorporados desde el PR #7

- Tarifa diaria configurable sin valor predeterminado.
- Tratamiento seguro del antiguo USD 12 pendiente de confirmación.
- Historial de tarifas con fecha y hora.
- Aportes de colaboradores reflejados en movimientos y financiación.
- Detalle multilínea en Inicio.
- Logotipo K&V en aplicación, instalador y portable.

## Seguridad de los datos

Antes de ejecutar una migración pendiente, la aplicación conserva su mecanismo de copia de seguridad previa. La base de datos permanece fuera de la carpeta reemplazada por el actualizador.

## Limitaciones

Esta es una versión preliminar y el instalador no está firmado digitalmente. Windows puede mostrar una advertencia de SmartScreen.
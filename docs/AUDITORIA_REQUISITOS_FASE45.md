# Auditoría de requisitos de la Fase 4.5

Fecha de validación: 20 de julio de 2026.

## Alcance protegido

La corrección se limita a **Uso del local** y **Perfil del trabajador**. No modifica Inicio, fórmulas financieras, esquema SQLite, instalación, actualización, exportación Excel ni otros módulos administrativos.

La revisión se realizó con una copia aislada de datos de alpha.1 en `.phase45-review-20260720`, dirigida mediante `PELUQUERIA_ADMIN_DATA_ROOT`. No se abrió ni modificó la base real instalada.

## Causa confirmada

- La misma propiedad de fecha se reutilizaba entre acciones y se guardaba directamente. Al cambiar a `Añadir trabajador`, una fecha anterior podía permanecer seleccionada y terminar registrada sin una señal visible.
- Un borrador recuperado restauraba esa fecha, pero no informaba explícitamente al usuario que el formulario provenía de una sesión anterior.
- El filtro general `Esta semana` también se aplicaba al historial del perfil. Por eso un pago del domingo 19 de julio no aparecía al revisar la semana iniciada el lunes 20, aunque sí participaba correctamente en la deuda acumulada.
- Una tarea diferida de autoguardado podía coincidir con el registro definitivo y volver a escribir un borrador ya finalizado.

La inspección de la copia de revisión confirmó que SQLite conservó exactamente la fecha `2026-06-16`; no hubo conversión de zona horaria ni alteración del motor de persistencia.

## Correcciones verificadas

- La fecha visible se enlaza en ambos sentidos y se actualiza inmediatamente.
- Cambiar de acción reinicia la fecha con el día local proporcionado por `TimeProvider`.
- Un borrador recuperado muestra: `Borrador recuperado: verifica la fecha visible antes de guardar.`
- Antes de registrar o limpiar se cancela y espera cualquier autoguardado diferido.
- El perfil usa un periodo independiente y abre por defecto en `Todo el historial`.
- Después de registrar un pago, el perfil vuelve a `Todo el historial` para mostrarlo sin depender del filtro general.
- `Deuda acumulada` conserva el saldo real; `Próximo pago requerido` presenta fecha e importe en el mismo bloque.
- Se retiró la tarjeta redundante `Importe estimado que faltará`.

## Evidencia funcional aislada

- Ingreso el `2026-07-20`: cero periodos completos y deuda `USD 0,00`.
- Ingreso el `2026-06-16`: cuatro periodos completos de siete días al corte del `2026-07-20`.
- Pago único el `2026-07-19` por `USD 12,00`: visible una vez en todo el historial.
- Deuda resultante: `USD 36,00`.
- Próximo pago requerido: `2026-07-04 · USD 12,00`.
- Integridad de SQLite: `ok`.

## Pruebas y calidad

- Debug: 135 pruebas superadas.
- Release: 135 pruebas superadas.
- Total de ejecuciones: 270.
- Compilación Debug y Release: cero advertencias y cero errores.
- `dotnet format --verify-no-changes`: correcto.
- Migraciones EF Core pendientes: ninguna.
- Vulnerabilidades NuGet directas o transitivas conocidas: ninguna.
- Revisión visual real en la segunda pantalla y pruebas de disposición equivalentes a 100 %, 125 % y 150 %: sin cortes ni superposiciones observados.

## Riesgos y asuntos futuros

No quedan defectos conocidos dentro del alcance de la Fase 4.5. Los escenarios de calendario, proyecciones, cambios de tarifa, retiros y crédito que merecen exploración adicional están separados en `COSAS_A_PROBAR_A_FUTURO.md`; su inclusión allí no los clasifica como fallos confirmados.

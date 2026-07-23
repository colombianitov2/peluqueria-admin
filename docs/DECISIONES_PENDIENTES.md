# Decisiones pendientes

Este registro impide que la implementación convierta supuestos en reglas de negocio. Las decisiones resueltas se conservan como trazabilidad; las demás no se consideran aprobadas hasta que el usuario las confirme.

## Decisiones resueltas en la Fase 0.1 (18 de julio de 2026)

- **Visibilidad del repositorio:** público.
- **Organización de publicación:** un único repositorio público para el código y los GitHub Releases.
- **Repositorio publicado el 18 de julio de 2026:** [https://github.com/colombianitov2/peluqueria-admin](https://github.com/colombianitov2/peluqueria-admin).
- **Tecnología adoptada:** C#, .NET 10, WPF y SQLite.
- **Actualizador previsto:** Velopack con GitHub Releases públicos.

Esta nota se conserva como antecedente histórico; esas funciones fueron autorizadas e implementadas en las fases posteriores.

## Decisiones resueltas en la Fase 2 (18 de julio de 2026)

- **Modelo monetario:** una sola moneda configurada para todo el local.
- **Moneda inicial:** USD.
- **Persistencia:** código monetario ISO de tres letras, normalizado en mayúsculas.
- **Límite funcional:** no se admiten varias monedas simultáneas, conversiones ni tasas de cambio.
- **Cambio de código:** modificar el código monetario no convierte automáticamente los valores existentes.
- **Persistencia local:** SQLite mediante Entity Framework Core, con los datos en `%LocalAppData%\PeluqueriaAdmin`.

La decisión de moneda queda cerrada. No define reglas semanales de cobro ni fórmulas financieras.

## Decisiones resueltas en la Fase 3 (18 de julio de 2026)

- **Compatibilidad inicial:** Windows 10 y Windows 11 de 64 bits, únicamente x64. Windows 11 es la plataforma principal de validación.
- **Nombre del módulo:** `Uso del local`.
- **Cuota semanal:** primera cuota en la fecha de ingreso y posteriores cada siete días, sin día fijo, sin prorrateo, anticipos ni saldos a favor.
- **Retiro:** no se generan periodos cuyo inicio sea posterior a la fecha de retiro; un periodo iniciado se cobra completo.
- **Histórico:** cada cuota conserva el importe vigente al generarse y los cambios de tarifa solo afectan cuotas futuras.
- **Pagos:** se admiten pagos parciales hasta el valor exacto de la deuda actual.
- **Colaboradores:** participan por defecto quienes estén vigentes al último día del mes; el cierre confirma participantes y conserva una fotografía inmutable sin prorrateo.
- **Obligaciones:** el punto de equilibrio usa el importe real si está completamente pagada y el esperado mientras esté pendiente o parcial, sin sumar ambos.
- **Copias:** máximo una automática diaria cuando cambie la base, retención de 30, copias diferenciadas antes de migraciones y respaldo previo a restaurar.
- **Firma:** el alfa se distribuye sin certificado; se documenta la advertencia posible de SmartScreen y se prepara configuración futura sin guardar certificados.
- **Actualizaciones:** Velopack 1.2.0 consulta GitHub Releases públicos sin token; una etiqueta SemVer `v*` deliberada activa el workflow de publicación.
- **Datos:** SQLite, copias y exportaciones viven bajo `%LocalAppData%\PeluqueriaAdmin`, fuera de la carpeta actualizada.

La distribución de residuos de centavos será determinista por identificador estable de participante para que la suma coincida exactamente con el fondo.

## Decisiones resueltas en la Fase 3.1 (18 de julio de 2026)

- **Cierres históricos:** un cierre confirmado es la fuente inmutable para resumen mensual, balance anual y CSV; la reapertura restaura el cálculo dinámico.
- **Reapertura:** se bloquea si existen pagos de distribución. Sin pagos, las asignaciones anteriores se invalidan lógicamente en la misma transacción.
- **Pagos calculados:** solo las asignaciones activas de cierres confirmados pueden pagarse.
- **Inicio:** se limita a servicios e impuestos pendientes hasta el fin del mes, deudas de Uso del local y faltante mensual.
- **Capacidad:** total, ocupación, disponibilidad y sobrecupo pertenecen únicamente a Uso del local.
- **Integridad histórica:** no se eliminan padres con dependencias, cierres ni asignaciones calculadas.
- **Informes:** el balance anual desglosa las categorías aprobadas y usa el indicador explícito `Positivo` o `Negativo`.

## Decisiones que permanecen abiertas

- Compatibilidad comprobada en equipos Windows 10 reales; por ahora solo se declara como objetivo.
- Certificado y proveedor de firma para una versión estable futura.
- Verificación de actualización entre dos Releases reales; no puede probarse sin publicar deliberadamente dos versiones.

## Decisiones resueltas en la Fase 4.7 (22 de julio de 2026)

- **Resumen real:** solo usa cuotas devengadas y cubiertas, ventas, otros ingresos, compras reales, gastos, imprevistos, pagos de obligaciones y mantenimientos realizados.
- **Fondo de colaboradores:** el porcentaje global crea el fondo; cada porcentaje individual representa una parte interna de máximo 100 % del fondo.
- **Histórico:** los meses terminados con movimientos se preservan automáticamente una sola vez, sin controles técnicos visibles.
- **Inventario:** `MonthlyRestockPlans` queda solo como compatibilidad histórica y no aparece ni afecta cálculos o Excel.
- **Inicio:** conserva únicamente la campana de mantenimiento; las obligaciones siguen en el bloque normal.
- **Notas:** bloc único persistente en SQLite, incluido en copias, restauración y Excel.
- **Formularios:** no existe una acción visible de limpieza; el éxito limpia los campos correspondientes y el error conserva la entrada.

## Decisiones resueltas en la Fase 4.8 (22 de julio de 2026)

- **Resultado repartible:** usa cobros operativos, egresos no provisionados, reservas, ajustes, cuotas de préstamos y compromisos anteriores; cuentas por cobrar y financiación quedan separadas.
- **Pérdidas:** el fondo y cada pago de colaborador son cero; ningún colaborador queda debiendo dinero.
- **Cierre mensual:** es manual y visible solo en Resumen mensual; congela cifras, reservas, exclusiones y porcentajes.
- **Pago a colaboradores:** se paga completo contra la asignación congelada; no admite importe arbitrario parcial.
- **Uso del local:** se elimina el retiro redundante visible; la eliminación lógica libera silla y conserva historia.
- **Inventario:** la Lista mensual de compra es una entidad nueva y no reutiliza planes de reposición.
- **Préstamos:** se administran dentro de Obligaciones y el desembolso se clasifica como financiación.
- **Balance anual:** muestra enero a diciembre y solo puede cerrarse con doce cierres mensuales.

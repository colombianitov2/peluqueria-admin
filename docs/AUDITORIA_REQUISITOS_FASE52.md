# Auditoría de requisitos — Fase 5.2

## Alcance

La Fase 5.2 reemplaza exclusivamente la regla de uso del local y corrige trazabilidad de aportes,
saldo disponible, detalle multilínea, identidad K&V y actualización. Inventario, obligaciones,
préstamos, mantenimiento, cierres y fórmulas no relacionadas permanecen protegidos.

## Decisión cerrada de recuperación 5.2.2 — valor heredado ambiguo

El 29 de julio de 2026 se confirmó que el modelo anterior no conserva procedencia suficiente para
distinguir el antiguo valor automático de USD 12 de un valor igual introducido por el administrador.
La política autorizada para la migración es:

- una instalación nueva conserva la tarifa diaria vacía, sin historial ni cargos hasta un guardado
  explícito;
- un valor heredado distinto de USD 12 se considera modificado expresamente, se conserva y comienza
  como tarifa diaria únicamente desde la actualización, sin recalcular periodos anteriores;
- un valor heredado exactamente igual a USD 12 se conserva como
  `Valor heredado pendiente de confirmación`, no crea tarifa diaria ni historial y no genera cargos
  nuevos;
- el administrador puede confirmar ese valor, cambiarlo por cualquier importe válido —incluido
  cero— o dejarlo vacío; solo la confirmación o el guardado explícito crea la primera tarifa diaria
  con vigencia desde su fecha y hora reales;
- el literal 12 se permite exclusivamente como marcador de clasificación durante esta migración.
  No puede permanecer como valor inicial, alternativa, fallback ni regla comercial.

La migración debe conservar intactos cargos, pagos, deudas consolidadas, créditos, saldos a favor,
tarifas semanales históricas y demás datos reales existentes.

## Causas raíz

| Hallazgo | Causa | Corrección |
|---|---|---|
| Detalle recortado en Inicio | fila con altura fija y celda sin ajuste | texto envolvente y `RowHeight=Auto` |
| Tarifa antigua en perfil | cálculo semanal y caché de vista | tarifa diaria histórica y notificación `DataChanged` |
| Anticipo inmóvil | proyección por ciclos semanales | consumo por cada día cobrable, excluyendo domingo |
| Aportes mezclados o duplicables | historial especializado sin evento financiero común y comando activo durante guardado | eventos separados, diferencia firmada y `CanExecute = !IsBusy` |
| Aporte fuera del saldo visible | financiación no mostrada en Inicio | `Saldo disponible del local` usa la caja mensual compartida |
| Icono genérico | no existía `ApplicationIcon` ni recurso WPF | JPEG original exacto y `.ico` de siete resoluciones |

## Persistencia

La migración `20260729100711_Phase52DailyFeesContributionsAndBranding` agrega:

- `DailyRates`;
- `DailyCharges`;
- `ChairAssignmentPeriods`;
- `FinancialEvents`.

La corrección `20260730020929_Phase522PendingLegacyDailyRate` hace nullable la tarifa, registra su
confirmación explícita y permite transiciones históricas a `Sin configurar`. `WeeklyRates` y
`WeeklyCharges` permanecen como legado. El valor heredado distinto de USD 12 comienza el día de la
actualización; el USD 12 ambiguo queda pendiente e inactivo. No se inventan cargos retroactivos.

## Verificación

Las pruebas automatizadas cubren instalación nueva vacía, legado USD 10, legado USD 12 pendiente,
confirmación explícita, ejemplos USD 50, tarifa cero, domingo, cambio 10 a 15, silla,
retiro, eliminación, pago parcial/anticipado, cruce mensual/anual, eventos y caja de aportes,
migración, Excel, refresco, envoltura de detalle e iconos. La evidencia final de Debug, Release,
seguridad, empaquetado, CI y actualización instalada se registra al cerrar la publicación.

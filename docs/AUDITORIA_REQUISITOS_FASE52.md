# Auditoría de requisitos — Fase 5.2

## Alcance

La Fase 5.2 reemplaza exclusivamente la regla de uso del local y corrige trazabilidad de aportes,
saldo disponible, detalle multilínea, identidad K&V y actualización. Inventario, obligaciones,
préstamos, mantenimiento, cierres y fórmulas no relacionadas permanecen protegidos.

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

No elimina tablas ni columnas. `WeeklyRates` y `WeeklyCharges` permanecen como legado. Si la base
solo conserva el valor actual y no una fecha histórica verificable, la tarifa diaria comienza el
día de la actualización y se registra esa limitación; no se inventan cargos retroactivos.

## Verificación

Las pruebas automatizadas cubren ejemplos USD 50, tarifa cero, domingo, cambio 10 a 15, silla,
retiro, eliminación, pago parcial/anticipado, cruce mensual/anual, eventos y caja de aportes,
migración, Excel, refresco, envoltura de detalle e iconos. La evidencia final de Debug, Release,
seguridad, empaquetado, CI y actualización instalada se registra al cerrar la publicación.

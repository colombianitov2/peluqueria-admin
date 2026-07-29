# Migración Fase 5.2

Migración EF: `20260729100711_Phase52DailyFeesContributionsAndBranding`.

## Política

- Es aditiva: no elimina ni renombra columnas o tablas existentes.
- Conserva `WeeklyRates` y `WeeklyCharges` como legado.
- No convierte cuotas semanales en cargos diarios.
- Crea una tarifa diaria inicial desde el valor actual de `Settings`.
- Cuando no existe fecha histórica verificable, usa la fecha de la actualización y deja un evento
  que documenta la limitación.
- Crea periodos vigentes solo para asignaciones actuales; no reconstruye ocupaciones anteriores.

## Validación requerida

La migración se ejecuta sobre una copia exacta de la base instalada. Antes y después se comparan
integridad, claves foráneas, migraciones, ajustes, notas y conteos de trabajadores, pagos,
colaboradores, aportes, cierres, inventario, obligaciones, préstamos y mantenimientos. La copia
validada no sustituye la base real.

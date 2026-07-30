# Migración Fases 5.2 y 5.2.2

Migración EF: `20260729100711_Phase52DailyFeesContributionsAndBranding`.
Corrección EF: `20260730020929_Phase522PendingLegacyDailyRate`.

## Política

- Es aditiva: no elimina ni renombra columnas o tablas existentes.
- Conserva `WeeklyRates` y `WeeklyCharges` como legado.
- No convierte cuotas semanales en cargos diarios.
- Una instalación nueva crea `Settings` con tarifa nula y sin filas en `DailyRates`.
- Un valor heredado distinto de USD 12 se considera modificado expresamente, se conserva y comienza
  a regir desde la actualización, sin recalcular periodos anteriores.
- Un valor heredado exactamente igual a USD 12 sin otra modificación demostrable queda conservado
  como pendiente de confirmación. La tarifa y el evento sintéticos de 5.2 se ocultan de las
  consultas activas; si una instalación anterior ya produjo cargos que los referencian, permanecen
  como tombstones técnicos para conservar la integridad referencial.
- Confirmar, modificar o vaciar el valor pendiente crea la primera transición explícita con fecha
  local efectiva y hora UTC de auditoría.
- USD 12 solo se usa en esta corrección para reconocer el legado ambiguo; no es valor inicial,
  fallback ni regla comercial.
- Crea periodos vigentes solo para asignaciones actuales; no reconstruye ocupaciones anteriores.

## Validación requerida

La migración se ejecuta sobre una copia exacta de la base instalada. Antes y después se comparan
integridad, claves foráneas, migraciones, ajustes, notas y conteos de trabajadores, pagos,
colaboradores, aportes, cierres, inventario, obligaciones, préstamos y mantenimientos. La copia
validada no sustituye la base real.

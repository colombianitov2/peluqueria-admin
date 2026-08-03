# Migración Fases 5.2 y 5.2.2

Migración EF: `20260729100711_Phase52DailyFeesContributionsAndBranding`.
Corrección EF: `20260730020929_Phase522PendingLegacyDailyRate`.
Hotfix EF: `20260802191907_Phase523CanonicalizeMigrationGuids`.

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

## Reparación de identificadores de Fase 5.2.3

Phase52 creó algunos identificadores desde SQL como 32 dígitos hexadecimales en minúscula. EF Core
10 para SQLite persiste los `Guid` como texto canónico de 36 caracteres, con guiones y letras
mayúsculas. Aunque ambos textos representan el mismo valor lógico, SQLite los compara como cadenas
diferentes y puede rechazar un cargo diario por su clave foránea.

La migración Phase523 valida primero todos los valores incluidos, detecta colisiones y después
normaliza únicamente columnas con semántica `Guid` de este grafo:

| Tabla | Columnas normalizadas | Relación |
|---|---|---|
| `Chairs` | `Id`, `AssignedPersonId` cuando existe | `AssignedPersonId` referencia `LocalUsePeople.Id` |
| `ChairAssignmentPeriods` | `Id`, `ChairId`, `PersonId` | `ChairId` referencia `Chairs.Id`; `PersonId` referencia `LocalUsePeople.Id` |
| `DailyRates` | `Id` | padre de `DailyCharges.RateId` |
| `DailyCharges` | `Id`, `PersonId`, `ChairId`, `RateId` | referencia trabajador, silla y tarifa diaria |
| `FinancialEvents` | `Id`, `OperationId`, `EntityId` | las dos últimas son referencias lógicas de auditoría |

`LocalUsePeople.Id` no fue creado por el SQL defectuoso y permanece sin cambios; sus referencias se
normalizan para coincidir con el formato que EF ya guardó en el padre. La reparación no genera
identificadores, no modifica datos comerciales y difiere —sin desactivar— la comprobación de claves
foráneas hasta el final de la transacción. Si encuentra un valor inválido o dos claves que
colisionarían al normalizarse, aborta todo el cambio.

`Down()` no reescribe datos: una vez normalizado un identificador no se puede distinguir con certeza
si antes estaba en formato correcto o heredado, y recrear el formato defectuoso sería destructivo.

## Validación requerida

La migración se ejecuta sobre una copia exacta de la base instalada. Antes y después se comparan
integridad, claves foráneas, migraciones, ajustes, notas y conteos de trabajadores, pagos,
colaboradores, aportes, cierres, inventario, obligaciones, préstamos y mantenimientos. La copia
validada no sustituye la base real.

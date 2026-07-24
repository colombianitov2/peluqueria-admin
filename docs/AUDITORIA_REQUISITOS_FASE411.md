# Correcciones de auditoría — Fase 4.11

Fecha: 24 de julio de 2026.

## Correcciones técnicas

- La primera cuota de uso del local se cobra el sábado inmediato y se prorratea por los días inclusivos desde el ingreso hasta ese sábado; las semanas posteriores cobran la tarifa completa y conservan la tarifa histórica vigente al inicio del periodo.
- Las cuentas por cobrar se calculan por trabajador; un saldo a favor no oculta la deuda de otra persona.
- Un compromiso excluido del cierre continúa apareciendo en cuentas por pagar; la exclusión solo evita crear la reserva de ese cierre.
- El Balance anual no vuelve a descontar cada mes el mismo compromiso arrastrado y reutiliza una sola fotografía por mes.
- Los saldos históricos de préstamos se reconstruyen con pagos fechados hasta el corte consultado.
- La parte del fondo de colaboradores sin porcentaje interno asignado vuelve a la utilidad retenida del local.
- Las copias usan SQLite Backup hacia un temporal validado antes de crear el archivo definitivo.
- Restaurar valida `integrity_check`, claves foráneas, migración base y ausencia de migraciones futuras; una copia antigua se migra en temporal antes del reemplazo.
- La copia automática considera cambios del archivo principal, WAL y SHM; se revisa al iniciar, periódicamente y al cerrar.
- El exportador CSV heredado deja de estar registrado en la aplicación. La interfaz conserva únicamente la exportación completa a Excel.
- Las acciones de GitHub quedan fijadas por SHA y el flujo de Release exige que la etiqueta apunte exactamente a la revisión actual de `main`.

## Limitaciones externas

- El instalador continúa sin firma digital hasta disponer de un certificado de firma de código válido. No se incluye ningún certificado o secreto en el repositorio.
- La actualización real entre dos Releases, Windows 10 físico, lector de pantalla y firma deben probarse fuera de la suite local.

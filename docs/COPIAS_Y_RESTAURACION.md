# Copias, restauración y exportación

## Ubicaciones

```text
%LocalAppData%\PeluqueriaAdmin\Data\peluqueria-admin.db
%LocalAppData%\PeluqueriaAdmin\Backups
%LocalAppData%\PeluqueriaAdmin\Exports
```

Estas carpetas están fuera de la instalación y no son reemplazadas por Velopack.

## Tipos de copia

- `auto-*`: como máximo una al día cuando la base cambió; se conservan las 30 automáticas más recientes.
- `manual-*`: creada con **Ajustes > Datos > Crear copia ahora**.
- `pre-migration-*`: creada antes de aplicar migraciones pendientes a un esquema existente.
- `pre-restore-*`: fotografía del estado actual antes de cada restauración.

La copia usa la API de respaldo en línea de SQLite, no una copia ciega mientras la base está abierta. Después se valida que existan las tablas y la migración base requeridas.

## Restaurar

1. En Ajustes, seleccione un archivo `.db`.
2. Pulse **Restaurar copia**.
3. La aplicación valida que sea una base compatible.
4. Se crea una copia `pre-restore-*` del estado actual.
5. Se cierran los pools SQLite, se reemplaza mediante archivo temporal y se vuelve a validar.
6. Si algo falla, se recupera la base anterior.
7. Reinicie la aplicación después de una restauración exitosa.

No seleccione una base desconocida ni cierre Windows durante el reemplazo. Las copias no se rastrean en Git.

## Exportación completa a Excel

**Exportar toda la información a Excel** crea un único libro real `.xlsx` con nombre `PeluqueriaAdmin-AAAA-MM-DD_HH-mm-ss.xlsx` en la carpeta configurada; de forma predeterminada usa el Escritorio. Si el nombre ya existe, añade un sufijo y nunca sobrescribe el archivo anterior. La interfaz no ofrece exportación CSV múltiple.

La exportación usa una única fecha de corte y una lectura transaccional consistente. Incluye datos actuales, históricos y futuros ya conocidos, `Movimientos generales`, tarifas semanales históricas, cuentas por cobrar/pagar, reservas, exclusiones, lista mensual, compras vinculadas, préstamos/cuotas, obligaciones y créditos con su recurrencia, cierres mensuales/anuales, saldos arrastrados, distribuciones congeladas, `Notas`, inventario acumulado, eliminaciones lógicas y borradores separados. La hoja `Planes de reposición` conserva la estructura anterior y `Compatibilidad inventario` identifica los indicadores antiguos de la lista mensual; ninguno se presenta como una operación vigente. Los textos peligrosos para fórmulas se escriben como texto. Primero se crea un temporal y solo se mueve a la carpeta final cuando el libro está completo; ante una falla se elimina el temporal.

El libro no modifica SQLite, no genera movimientos, no importa datos y no requiere Microsoft Excel instalado. La copia y restauración de SQLite incluye automáticamente `Notes` junto con el resto de tablas. El Manual forma parte del ejecutable y no se duplica como dato del negocio. Al finalizar, Ajustes muestra la ruta y habilita **Abrir archivo** y **Abrir carpeta**.

## Verificación realizada

Las pruebas crean una base controlada dentro de resultados de prueba, generan una copia, modifican Ajustes, restauran y confirman el valor anterior. También verifican un solo `.xlsx`, nombre único, hojas completas, filtros, filas inmovilizadas, tipos de fecha/dinero/porcentaje, tarifas, lista mensual, compatibilidad, Crédito/Semanal, resumen, balance, eliminados, borradores, limpieza del temporal y ausencia de CSV. No se usan datos reales ni el Escritorio real.

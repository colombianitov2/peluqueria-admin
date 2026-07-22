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

**Exportar toda la información a Excel** crea un único libro real `.xlsx` con nombre `Peluqueria-Administracion-AAAA-MM-DD-HHmmss.xlsx` en la carpeta configurada; de forma predeterminada usa el Escritorio. Si el nombre ya existe, añade un sufijo y nunca sobrescribe el archivo anterior. La interfaz no ofrece exportación CSV múltiple.

La exportación usa una única fecha de corte y una lectura transaccional consistente. Incluye datos actuales, históricos y futuros ya conocidos, snapshots de cierres, desglose anual, inventario acumulado, eliminaciones lógicas y, en una hoja separada, borradores que aún no son operaciones registradas. Los textos peligrosos para fórmulas se escriben como texto. Primero se crea un temporal y solo se mueve al Escritorio cuando el libro está completo; ante una falla se elimina el temporal.

El libro no modifica SQLite, no genera movimientos, no importa datos y no requiere Microsoft Excel instalado. Al finalizar, Ajustes muestra la ruta y habilita **Abrir archivo** y **Abrir carpeta**.

## Verificación realizada

Las pruebas crean una base controlada dentro de resultados de prueba, generan una copia, modifican Ajustes, restauran y confirman el valor anterior. También verifican un solo `.xlsx`, hojas, tipos, ruta y ausencia de CSV. No se usaron datos reales.

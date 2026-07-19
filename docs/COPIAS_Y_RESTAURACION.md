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

## Exportar CSV

**Exportar CSV** genera cinco archivos UTF-8 sin BOM para el año actual:

- resumen mensual;
- balance anual;
- flujo de caja;
- inventario actual;
- deudas por Uso del local.

Los campos se escapan según CSV y los importes se expresan con punto decimal. La exportación es una fotografía de consulta: no modifica SQLite y no requiere Excel.

## Verificación realizada

Las pruebas crean una base controlada dentro de resultados de prueba, generan una copia, modifican Ajustes, restauran y confirman el valor anterior. También verifican la creación y codificación de los cinco CSV. No se usaron datos reales.


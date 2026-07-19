# Requisitos vigentes

## Carácter canónico y precedencia

Este documento es la fuente canónica actual de requisitos e incorpora las decisiones aprobadas hasta la Fase 4.2 del 19 de julio de 2026.

La Fase 4.2 sustituye expresamente, cuando exista contradicción, las reglas anteriores sobre terminología del personal, cobro semanal, pantallas genéricas de Uso del local y Colaboradores, inventario heredado, aportes de capital, número abstracto de sillas, módulo visible Flujo de caja y lista exclusiva anterior de Inicio. La excepción autorizada en Inicio es únicamente el precio sugerido por silla.

Los trabajadores son quienes usan y alquilan las sillas. Los colaboradores son exclusivamente inversionistas y nunca ocupan sillas. La interfaz, los mensajes, Excel y la documentación usan esta distinción.

- Cuando exista contradicción con ideas iniciales de `Programa para peluquería.txt`, prevalece este documento.
- `Instrucciones codex.txt` conserva las normas de trabajo y seguridad del proyecto, excepto cuando contradiga expresamente la solicitud vigente.
- Los dos archivos anteriores no estaban presentes en la carpeta del proyecto durante esta preparación. Su contenido deberá revisarse antes de comenzar la implementación para confirmar que no haya contexto adicional compatible con estos requisitos.
- No deben inventarse funciones, reglas de negocio, datos ni obligaciones que no aparezcan aquí o que el usuario no apruebe posteriormente.

## 1. Propósito y límites

La aplicación es una herramienta interna de administración para una peluquería. Su finalidad es registrar y clasificar:

- cuánto dinero entra;
- cuánto dinero sale;
- qué obligaciones existen;
- qué inventario queda;
- si el local alcanza su punto de equilibrio.

La aplicación:

- no es un sistema contable oficial;
- no está dirigida a una empresa colombiana;
- no implementará normativa fiscal colombiana;
- no preparará declaraciones de impuestos;
- no presentará información ante entidades gubernamentales.

## 2. Modelo del negocio

- Las personas que trabajan en el local llevan sus propios implementos y productos de trabajo.
- El local no paga esos implementos ni productos.
- El dinero que estas personas cobran a sus clientes por sus servicios no pertenece al local y no se registra como ingreso del local.
- Cada persona paga actualmente USD 12 semanales por utilizar el local y guardar sus pertenencias de trabajo.
- Los USD 12 constituyen un valor general configurable en Ajustes, no una tarifa individual.
- El día habitual de pago es sábado.
- Al ingresar la deuda es cero. Cada cuota corresponde a siete días completos; vence el primer sábado igual o posterior al final del periodo, no se cobra un periodo incompleto y se conserva la tarifa histórica.
- Los pagos registrados reducen la deuda de cada persona.
- La página principal muestra el nombre de cada persona con deuda y el importe adeudado.

## 3. Personas que pagan por utilizar el local

El módulo se llama **Uso del local** y no representa una relación laboral.

Datos mínimos previstos:

- nombre;
- fecha de ingreso;
- fecha de retiro, cuando corresponda.
- descripción opcional;
- silla individual asignada actualmente.

La condición activa o inactiva se calcula internamente a partir de las fechas; no existirá un campo visible de estado.

No incluir:

- fotografía;
- espacio de almacenamiento asignado;
- tarifa semanal individual;
- campo visible activo/inactivo;
- semanas pagadas por anticipado;
- semanas perdonadas;
- semanas suspendidas;
- motivos de suspensión;
- comprobantes;
- recibos internos;
- estados vencido, exonerado o anulado;
- medio de pago;
- consultas periódicas constantes.

El registro de un pago será simple y contendrá únicamente:

- persona;
- fecha;
- monto.
- descripción opcional.

## 4. Sillas y capacidad

Cada silla es un registro individual con nombre o número, fecha de creación, descripción opcional y, si corresponde, un único trabajador vigente asignado. Los colaboradores son inversionistas y nunca ocupan sillas. Se muestran total de sillas, trabajadores vigentes y sillas disponibles.

No incluir:

- historial de ocupación;
- ubicación de silla;
- espacio de almacenamiento;
- mantenimiento individual de silla dentro de esta sección.

## 5. Ingresos del local

Los ingresos propios del local pueden provenir de:

- pagos semanales por uso del local;
- venta de agua, gaseosas u otros productos;
- venta futura de productos de belleza;
- otros ingresos registrados manualmente.

Los servicios prestados directamente por las personas a sus clientes no se registran como ingreso del local.

## 6. Inventario

El inventario usa exclusivamente estas categorías: Alimento o bebida para venta, Otro producto para venta, Cortesía para clientes, Aseo, Insumo del local y Otro producto del local. No se expone el antiguo atributo técnico de unidades en interfaz, CSV, Excel ni formularios.

Los productos destinados a venta aparecen inmediatamente en Ventas, pueden buscarse por nombre sin distinguir mayúsculas y muestran existencia y precio predeterminado. Cambiar a una categoría no vendible los retira del selector; cambiar a una categoría vendible los incorpora tras guardar, sin reiniciar.

## 6.1 Uso del local y perfiles

Uso del local conserva las tres tarjetas y la actividad filtrada, pero usa tablas independientes de trabajadores y sillas. El selector **Acción** contiene únicamente **Añadir silla** y **Añadir trabajador**. El perfil de cada trabajador se abre por doble clic dentro del módulo y reúne datos, silla, tarifas históricas, deuda, pago, asignación/retiro y un historial cronológico virtualizado.

## 6.2 Aportes de colaboradores

Cada colaborador dispone de un perfil con aportes de capital, participaciones de cierres y distribuciones. Los aportes son inversión no operativa: no son ventas ni otros ingresos, no aumentan la ganancia neta, no generan un nuevo porcentaje y no alteran el punto de equilibrio. Se conservan mediante eliminación lógica y se incluyen en copias, CSV, Excel e historial.

Los productos personales de quienes trabajan en el local no pertenecen al inventario.

El inventario funciona mediante movimientos y conteos físicos mensuales:

- existencia inicial;
- compra o entrada;
- venta;
- consumo interno;
- ajuste por conteo físico;
- existencia resultante.

Reglas:

- El sobrante de un mes pasa al siguiente.
- El sobrante no se registra nuevamente como compra ni como gasto.
- Una compra afecta el dinero disponible solamente en el mes en que realmente se pagó.
- El conteo mensual no crea un gasto.
- La reposición se calcula con base en lo que realmente falta, sin repetir los productos que todavía existen.

No incluir en productos o ventas:

- código;
- stock objetivo;
- proveedor;
- vencimiento;
- stock mínimo;
- estado activo/inactivo;
- campos adicionales distintos de la descripción opcional autorizada.

No incluir en ninguna sección proveedor, medio de pago ni comprobante.

## 7. Gastos

Clasificar, como mínimo:

- servicios y obligaciones recurrentes;
- insumos obligatorios;
- insumos opcionales para clientes;
- compras de productos para la venta;
- mantenimiento;
- gastos imprevistos;
- otros gastos.

Los gastos imprevistos pueden añadirse en cualquier mes para daños, reparaciones o acontecimientos no planificados.

## 8. Servicios, obligaciones e impuestos

- Los servicios y obligaciones se registran manualmente con sus fechas y valores.
- Los impuestos son únicamente recordatorios y gastos internos.
- La aplicación no calcula obligaciones legales ni prepara declaraciones.
- La página principal muestra solamente la fecha y el nombre de los servicios o impuestos pendientes de pago, junto con los demás elementos expresamente permitidos para esa página.

## 9. Mantenimiento

Debe existir una sección de mantenimiento para:

- aires acondicionados;
- equipos del local;
- otros bienes que requieran mantenimiento.

Datos que se pueden registrar:

- equipo o bien;
- tipo de mantenimiento;
- fecha prevista;
- costo estimado, aunque inicialmente sea desconocido;
- fecha realizada;
- costo real cuando se conozca.

No incluir:

- ubicación;
- estado manual;
- técnico;
- proveedor;
- campos adicionales distintos de la descripción opcional autorizada.

La necesidad de atención se calcula a partir de las fechas y de la existencia o ausencia de costo y fecha real, sin un campo manual de estado.

## 10. Punto de equilibrio mensual

El punto de equilibrio se maneja por mes y compara el dinero realmente ingresado contra las obligaciones y gastos del mes mediante un enfoque sencillo de flujo de dinero.

Ingresos del mes:

- pagos recibidos por uso del local;
- ingreso bruto recibido por ventas;
- otros ingresos.

Salidas del mes:

- servicios y obligaciones pagados o presupuestados, según corresponda;
- insumos obligatorios comprados;
- insumos opcionales comprados o presupuesto opcional configurado;
- compras de mercancía;
- mantenimiento;
- gastos imprevistos;
- otros gastos.

Reglas:

- Las compras de inventario se cuentan solamente cuando se realizan.
- El inventario sobrante no vuelve a contarse como gasto.
- No se mezclan ingresos por servicios personales de quienes trabajan en el local.

Mostrar:

- total requerido del mes;
- total ingresado;
- cantidad faltante para llegar al punto de equilibrio;
- resultado positivo o negativo.

## 11. Colaboradores y distribución de ganancias

Los colaboradores forman un grupo distinto de las personas que pagan por utilizar el local. Reciben conjuntamente un porcentaje de la ganancia mensual positiva.

- Porcentaje inicial: 20 %.
- El porcentaje es configurable en Ajustes con el nombre **Ganancia colaboradores**.
- La sección puede llamarse **Nómina de colaboradores**, pero no se trata como nómina laboral ni aplica normas laborales.

Fórmulas:

```text
resultado base =
  ingresos del local
  - gastos y obligaciones correspondientes
  - presupuesto o gasto opcional aplicable
```

Si el resultado base es menor o igual a cero:

- ganancia de colaboradores = 0;
- no se crea deuda con los colaboradores;
- las obligaciones indispensables pendientes continúan apareciendo por separado.

Si el resultado base es positivo:

```text
fondo colaboradores = resultado base × porcentaje configurado
pago por colaborador = fondo colaboradores ÷ cantidad de colaboradores correspondientes
resultado retenido por el local = resultado base - fondo colaboradores
```

No se inventará una fórmula circular.

El porcentaje de colaboradores no modifica el punto exacto donde el resultado es cero, porque cualquier porcentaje de cero sigue siendo cero. Sí modifica cuánto conserva el local después de obtener una ganancia.

## 12. Ajustes

Configurar, como mínimo:

- valor semanal general por uso del local, inicialmente USD 12;
- porcentaje de ganancia de colaboradores, inicialmente 20 %;
- presupuesto mensual para insumos opcionales ofrecidos a clientes;
- moneda principal, inicialmente USD.
- gastos extraoficiales separados, que solo intervienen en el precio sugerido por silla.

No se crean ajustes individuales que contradigan la tarifa semanal general.

La moneda es única para todo el local y se selecciona inicialmente entre USD y COP. No existen conversiones, tasas de cambio ni varias monedas simultáneas. Cambiar el código no convierte los importes existentes.

Los importes de Ajustes se persisten en unidades menores enteras y los porcentajes en puntos básicos. No se usa punto flotante binario ni se aceptan silenciosamente más de dos decimales.

## 13. Balance anual

El balance por año muestra:

- ingresos acumulados;
- gastos acumulados por categoría;
- distribuciones pagadas a colaboradores;
- impuestos u obligaciones anuales;
- resultado retenido por el local;
- indicador positivo o negativo.

El indicador mensual es negativo cuando todavía falta dinero para cubrir las obligaciones mensuales. El indicador anual es negativo cuando el resultado acumulado, incluidas las obligaciones e impuestos anuales registrados, es inferior a cero.

## 14. Página principal

La página principal muestra exclusivamente:

- fecha actual o mes seleccionado;
- fecha y nombre de servicios e impuestos pendientes de pago;
- nombre de cada persona que debe pagos por uso del local;
- monto adeudado por cada persona;
- cantidad faltante para alcanzar el punto de equilibrio mensual.
- precio semanal actual, precio semanal sugerido por silla ocupada y equivalente mensual, con explicación breve.

No mostrar allí:

- gráficos;
- inventario;
- alertas de inventario;
- ventas;
- mantenimiento;
- elementos distintos del precio sugerido expresamente autorizado.
- nómina de colaboradores;
- tarjetas o indicadores adicionales.

## 15. Datos y seguridad

- La aplicación administra sus datos localmente y funciona sin Internet para esa administración.
- Se usará una base de datos local adecuada, preferentemente SQLite, salvo que un análisis técnico demuestre una alternativa mejor.
- Todo registro se puede editar.
- Las eliminaciones conservan historial mediante eliminación lógica interna, sin obligar a mostrar un campo de estado en la interfaz.
- Se agregan internamente fechas de creación, modificación y eliminación cuando sean necesarias.
- La base de datos real, sus copias de seguridad, archivos personales, contraseñas, tokens y certificados no se guardan en Git.

Copias y exportación:

- máximo una copia automática diaria cuando la base cambió y retención de las 30 automáticas más recientes;
- copia diferenciada antes de migrar un esquema existente y antes de restaurar;
- restauración manual después de validar compatibilidad y con recuperación de la base anterior ante fallo;
- exportación CSV UTF-8 de resumen mensual, balance anual, flujo de caja, inventario y deudas por Uso del local.

La arquitectura debe contemplar:

- copias de seguridad automáticas;
- restauración manual;
- protección de datos durante actualizaciones;
- migraciones transaccionales de base de datos;
- copia de seguridad previa a migraciones importantes;
- posibilidad futura de exportar información.

## 16. Instalación y actualizaciones

El programa está dirigido inicialmente a Windows.

Primera instalación:

- debe existir un instalador ejecutable.

Después de instalado, la aplicación debe:

- buscar nuevas versiones publicadas mediante GitHub;
- descargar una actualización disponible;
- aplicarla al cerrar o reiniciar la aplicación;
- actualizarse sin exigir una desinstalación manual previa;
- conservar siempre la base de datos del usuario;
- ofrecer una acción manual **Buscar actualizaciones**.

Política de versiones y publicación:

- usar versiones semánticas;
- preparar versiones estables mediante GitHub Releases;
- automatizar compilación, pruebas, empaquetado y publicación exclusivamente mediante etiquetas SemVer `v*` deliberadas;
- no incrustar un token personal de GitHub en el ejecutable.

El proyecto utiliza un único repositorio público para el código y los lanzamientos:

- [https://github.com/colombianitov2/peluqueria-admin](https://github.com/colombianitov2/peluqueria-admin)
- El repositorio remoto ya existe y el proyecto fue publicado el 18 de julio de 2026.
- Los GitHub Releases públicos del mismo repositorio serán el canal previsto para las actualizaciones.

El ejecutable nunca debe incluir un token personal de GitHub ni otra credencial para consultar o descargar actualizaciones públicas.

La primera alpha es x64, sin certificado y puede activar una advertencia de SmartScreen. Windows 11 es la plataforma principal de validación; Windows 10 x64 sigue siendo un objetivo no verificado en un equipo real. No se declara verificada una actualización entre Releases hasta disponer de dos versiones publicadas.

## 17. Correcciones de aceptación de la Fase 3.1

- Editar y eliminar son acciones separadas: la confirmación se exige solo para eliminar y se reinicia después de usarla.
- Al crear una persona de Uso del local se generan inmediatamente sus cuotas aplicables. Al crear una obligación recurrente se generan inmediatamente sus ocurrencias aplicables, sin reiniciar la aplicación y sin duplicados.
- Una recurrencia mensual conserva como ancla la fecha original; por ejemplo, una obligación del día 31 pasa por el último día de febrero y vuelve al día 31 cuando el mes lo permite.
- Un cambio de ingreso o retiro que invalide cuotas con pagos se rechaza. Sin pagos, las cuotas incompatibles pueden invalidarse de forma lógica y transaccional.
- Reabrir un cierre con pagos de distribución se rechaza. Sin pagos, la reapertura invalida sus asignaciones calculadas; un cierre nuevo crea una sola distribución activa cuya suma coincide exactamente con el fondo.
- Solo las asignaciones de cierres confirmados se pueden pagar o mostrar como pendientes.
- Un cierre confirmado es una fotografía histórica para el resumen mensual, el balance anual y los CSV. Un mes reabierto vuelve al cálculo dinámico.
- Inicio muestra exclusivamente servicios e impuestos pendientes vencidos o del mes actual, deudas por Uso del local y el faltante mensual.
- La capacidad de sillas se muestra únicamente en Uso del local, incluyendo total, personas vigentes, disponibles y sobrecupo explícito.
- El balance anual y su CSV desglosan las categorías aprobadas y muestran un indicador explícito `Positivo` o `Negativo`.
- Las correcciones de inventario conservan las invariantes de cantidad, dinero y existencia cronológica no negativa. Los nombres activos de productos son únicos sin distinguir mayúsculas.
- No se permite eliminar padres con historial dependiente ni registros calculados como cierres o asignaciones. Los datos históricos huérfanos heredados se muestran con una descripción segura en vez de cerrar la pantalla.
- Los estados y categorías visibles y exportados se presentan en español.

## 18. Correcciones de aceptación de la Fase 4.1

- Todas las páginas operativas muestran actividad no editable con periodo Hoy por defecto, semana, mes, 3 meses, 6 meses, año y rango personalizado. El cambio de día se detecta al actualizar por navegación, periodo u operación; no existe sondeo constante.
- Las operaciones confirmadas crean su actividad en la misma transacción. Los estados actuales y selectores no dependen del filtro de actividad.
- La recuperación de formularios es silenciosa; la interfaz solo ofrece `Limpiar formulario` cuando hay contenido no confirmado.
- Ventas selecciona por identificador un producto de venta, usa su precio predeterminado y rechaza inventario negativo. Las compras reutilizan productos existentes y calculan su total.
- Ingresos, gastos, imprevistos, obligaciones y mantenimiento usan acciones directas; no existe un desplegable genérico Acción.
- Los colaboradores no ocupan sillas y su historial financiero se deriva únicamente de cierres, participaciones y pagos reales.
- Resumen mensual añade gráficos 2D de barras, composición y evolución con los mismos cálculos que las cifras.
- Flujo de caja se retira de navegación, pantallas, manual y hoja independiente de Excel; se conservan las operaciones fuente.
- La exportación Excel incluye sillas, asignaciones, actividad, descripciones, gastos extraoficiales, precio sugerido e historial financiero de colaboradores.
- El futuro módulo Manual queda como requisito pendiente y no se muestra un botón vacío.

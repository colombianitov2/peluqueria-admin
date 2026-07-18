# Decisiones pendientes

Este registro impide que la implementación convierta supuestos en reglas de negocio. Las decisiones resueltas se conservan como trazabilidad; las demás no se consideran aprobadas hasta que el usuario las confirme.

## Decisiones resueltas en la Fase 0.1 (18 de julio de 2026)

- **Visibilidad del repositorio:** público.
- **Organización de publicación:** un único repositorio público para el código y los GitHub Releases.
- **Repositorio publicado el 18 de julio de 2026:** [https://github.com/colombianitov2/peluqueria-admin](https://github.com/colombianitov2/peluqueria-admin).
- **Tecnología adoptada:** C#, .NET 10, WPF y SQLite.
- **Actualizador previsto:** Velopack con GitHub Releases públicos.

La creación de la solución base queda autorizada en la Fase 1. La implementación de módulos funcionales, SQLite, Velopack y paquetes externos continúa fuera del alcance de esta fase.

## Decisiones resueltas en la Fase 2 (18 de julio de 2026)

- **Modelo monetario:** una sola moneda configurada para todo el local.
- **Moneda inicial:** USD.
- **Persistencia:** código monetario ISO de tres letras, normalizado en mayúsculas.
- **Límite funcional:** no se admiten varias monedas simultáneas, conversiones ni tasas de cambio.
- **Cambio de código:** modificar el código monetario no convierte automáticamente los valores existentes.
- **Persistencia local:** SQLite mediante Entity Framework Core, con los datos en `%LocalAppData%\PeluqueriaAdmin`.

La decisión de moneda queda cerrada. No define reglas semanales de cobro ni fórmulas financieras.

## Decisiones pendientes antes de implementar

### 1. Generación del cobro semanal

Se debe definir:

- qué intervalo representa una semana cobrable;
- en qué momento se genera la obligación;
- cómo se trata la semana de ingreso y la semana de retiro;
- cómo se comporta el cálculo cuando la fecha de ingreso o retiro cae a mitad de semana.

**Restricción vigente:** el sábado no está confirmado como día de generación o cobro. No se asumirá ningún día.

### 2. Versiones de Windows compatibles

Definir las versiones y arquitecturas mínimas admitidas, por ejemplo:

- Windows 10 y Windows 11, o solamente Windows 11;
- procesadores x64 y, si se requiere, ARM64.

Esta decisión afecta el instalador, las pruebas, el soporte y la estrategia de empaquetado.

## Decisiones adicionales que conviene cerrar

### 3. Nombre del módulo de personas que pagan por usar el local

El nombre **Trabajadores y alquiler de sillas** está prohibido. Debe elegirse un nombre definitivo que no confunda este grupo con los colaboradores.

### 4. Colaboradores correspondientes a cada mes

Se debe definir cómo se determina quiénes participan en el reparto de un mes cuando una persona entra o sale durante ese mes. No se deben inventar prorrateos ni reglas laborales.

### 5. Importes pagados y presupuestados en el punto de equilibrio

Se debe precisar, por cada tipo de servicio u obligación, cuándo se usa el valor pagado y cuándo el presupuestado para evitar dobles conteos.

### 6. Política de copias de seguridad

Definir la frecuencia, cantidad de copias a conservar y ubicación elegida por el usuario. La arquitectura ya exige copia previa a migraciones importantes y restauración manual segura.

### 7. Firma de código

Decidir, antes de una distribución real, si los instaladores y ejecutables se firmarán con un certificado de firma de código.

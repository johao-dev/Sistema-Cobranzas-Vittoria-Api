# Guía del proyecto para agentes

## 1. Resumen del proyecto y objetivos

### Qué hace el proyecto

- **Nombre:** Sistema de Gastos Vittoria.
- **Descripción:** Sistema interno para gestionar operaciones relacionadas con obras, compras, inventario, gastos y control presupuestario.
- **Usuarios principales:** Administrador, Residente, Almacen, Coordinador, Comprador.
- **Funcionalidades principales:** Flujo de Requerimientos -> OC -> Compras, Gastos Directos y Reporte de control presupuestario.
- **Objetivos:** Convertirse en el ERP interno de la empresa.

### Stack tecnológico

- **Lenguaje y versión:** C#.
- **Framework y versión:** .NET 8 con ASP.NET Core.
- **Base de datos:** SQL Server Express 2025.
- **Acceso a datos:** Dapper.
- **Pruebas:** NUnit, Respawn, Testcontainers.

## 2. Reglas de arquitectura y código

### Patrones de diseño

- **Arquitectura:** Arquitectura en capas legacy - Migrando a arquitectura de tipo monolito modular usando featured based a carpetas superiores y clean architecture en carpetas inferiores.
- **Dependencias permitidas:** La misma regla de dependencia que la arquitectura limpia.
- **Patrones utilizados:** GoF.
- Respetar los patrones existentes y evitar abstracciones sin una necesidad concreta.

### Manejo de errores

- **Estrategia general:** Todos los errores de la API se manejan usando el ApiMiddlewareException, el resto solo propaga.
- **Validaciones:** Entradas HTTP se validan mediante DataAnnotations en los DTOs. Validaciones de negocio interna son clases dedicadas.
- **Respuestas de error:** Aún en desarrollo Mayormente se usa Código y Mensaje.
- **Registro de errores:** Mediante logs.
- No silenciar excepciones ni exponer datos sensibles en mensajes o registros.

### Convenciones sobre nombres

- **Archivos y carpetas:** Carpetas en PascalCase en singular y carpetas de acciones usando verbos. Archivos usa la convención propia de C# y .NET 8.
- **Clases, interfaces y tipos:** Se usa 'I' para interfaces.
- **Variables, propiedades y constantes:** Variables en general en lowerCamelCase, constantes en UPPER_SNAKE_CASE.
- **Idioma del código:** Código y comentarios en español. Mantener el código "spanglish" donde corresponda.
- Usar nombres descriptivos y mantener consistencia con el código existente.

### Código limpio y SOLID

- Seguir principios de código limpio y SOLID cuando sea posible y aporten claridad.
- Mantener clases y métodos enfocados en una responsabilidad concreta.
- Evitar duplicación, complejidad innecesaria y código que no se utiliza.
- Favorecer interfaces pequeñas y dependencias explícitas cuando corresponda.
- Mantener contratos coherentes para que las implementaciones sean sustituibles.
- Escribir comentarios que expliquen decisiones o motivos, sin repetir lo evidente del código.
- Adaptar estas prácticas al contexto del proyecto sin introducir abstracciones innecesarias.

using System;
using System.IO;

namespace L
{
    /// <summary>
    /// Logger centralizado. Escribe en archivo y en consola.
    /// No depende de ninguna otra capa (autónoma).
    /// Registrar como Singleton en DI.
    /// </summary>
    public class AppLogger
    {
        private readonly string _rutaArchivo;
        private static readonly object _lock = new object();

        public AppLogger(string rutaArchivo)
        {
            _rutaArchivo = rutaArchivo;

            var directorio = Path.GetDirectoryName(rutaArchivo);
            if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
                Directory.CreateDirectory(directorio);
        }

        public void LogInfo(string mensaje)    => Escribir("INFO ", mensaje);
        public void LogWarning(string mensaje) => Escribir("WARN ", mensaje);

        public void LogError(string mensaje, Exception ex = null)
        {
            var detalle = ex is null
                ? mensaje
                : $"{mensaje} | Excepción: {ex.Message} | StackTrace: {ex.StackTrace}";
            Escribir("ERROR", detalle);
        }

        private void Escribir(string nivel, string mensaje)
        {
            var linea = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{nivel}] {mensaje}";
            lock (_lock)
            {
                File.AppendAllText(_rutaArchivo, linea + Environment.NewLine);
            }
            Console.WriteLine(linea);
        }
    }
}

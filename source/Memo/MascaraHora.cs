using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Memo
{
    /// <summary>
    /// Transforma um TextBox num campo de hora HH:mm amigável: aceita só dígitos,
    /// insere o ":" sozinho e, ao sair, completa/valida (24h). Digite "900" → "09:00".
    /// </summary>
    internal static class MascaraHora
    {
        public static void Aplicar(TextBox campo)
        {
            campo.MaxLength = 5;
            var atualizando = false;

            // Só dígitos podem ser digitados.
            campo.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);

            // Colar: mantém apenas os dígitos.
            DataObject.AddPastingHandler(campo, (_, e) =>
            {
                if (e.DataObject.GetDataPresent(DataFormats.Text))
                {
                    var texto = (string)e.DataObject.GetData(DataFormats.Text);
                    var digitos = new string((texto ?? "").Where(char.IsDigit).ToArray());
                    campo.Text = Formatar(digitos);
                    campo.CaretIndex = campo.Text.Length;
                }
                e.CancelCommand();
            });

            // Formata enquanto digita (insere o ":").
            campo.TextChanged += (_, __) =>
            {
                if (atualizando) return;
                var formatado = Formatar(new string(campo.Text.Where(char.IsDigit).ToArray()));
                if (formatado == campo.Text) return;

                atualizando = true;
                campo.Text = formatado;
                campo.CaretIndex = formatado.Length;
                atualizando = false;
            };

            // Ao sair, completa e clampa (ex.: "9" → "09:00").
            campo.LostFocus += (_, __) =>
            {
                var normal = Normalizar(campo.Text);
                if (normal != null && normal != campo.Text) campo.Text = normal;
            };
        }

        private static string Formatar(string digitos)
        {
            if (digitos.Length > 4) digitos = digitos.Substring(0, 4);
            return digitos.Length <= 2 ? digitos : digitos.Substring(0, 2) + ":" + digitos.Substring(2);
        }

        /// <summary>Completa/clampa para HH:mm; null se estiver vazio.</summary>
        private static string Normalizar(string texto)
        {
            var digitos = new string((texto ?? "").Where(char.IsDigit).ToArray());
            if (digitos.Length == 0) return null;

            int hora, minuto = 0;
            if (digitos.Length <= 2)
            {
                hora = int.Parse(digitos);
            }
            else
            {
                hora = int.Parse(digitos.Substring(0, 2));
                minuto = int.Parse(digitos.Substring(2).PadRight(2, '0'));
            }

            hora = Math.Min(23, Math.Max(0, hora));
            minuto = Math.Min(59, Math.Max(0, minuto));
            return $"{hora:00}:{minuto:00}";
        }
    }
}

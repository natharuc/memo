using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using Memo.Service.Auth;
using OpenCvSharp;
using ZXing;

namespace Memo
{
    public partial class JanelaAuthAdicionar : System.Windows.Window
    {
        private readonly List<string> _uris = new List<string>();
        private VideoCapture _camera;
        private volatile bool _cameraViva;
        private int _enviando;

        public JanelaAuthAdicionar()
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);
            Closed += (_, __) => PararCamera();
        }

        private void Camera_Click(object sender, RoutedEventArgs e)
        {
            LimparErro();
            PararCamera();
            try
            {
                _camera = new VideoCapture(0);
                if (!_camera.IsOpened())
                {
                    Erro("Não abri a câmera. Cole um print ou escolha um arquivo.");
                    PararCamera();
                    return;
                }
            }
            catch (Exception ex)
            {
                Erro("Não abri a câmera: " + ex.Message);
                PararCamera();
                return;
            }

            textoPreview.Text = "Aponte para o QR code…";
            textoPreview.Visibility = Visibility.Visible;
            _cameraViva = true;
            var cam = _camera;
            ThreadPool.QueueUserWorkItem(_ => LoopCamera(cam));
        }

        private void LoopCamera(VideoCapture cam)
        {
            using (var mat = new Mat())
            {
                while (_cameraViva && ReferenceEquals(cam, _camera))
                {
                    try
                    {
                        if (!cam.Read(mat) || mat.Empty())
                        {
                            Thread.Sleep(80);
                            continue;
                        }
                        using (var copia = mat.Clone())
                        {
                            var uris = DecodificarMat(copia);
                            var previa = copia.Clone();
                            Dispatcher.BeginInvoke(new Action(() => MostrarMat(previa)));
                            if (uris.Count > 0)
                            {
                                _cameraViva = false;
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    PararCamera();
                                    Aceitar(uris);
                                }));
                                return;
                            }
                        }
                    }
                    catch
                    {
                        Thread.Sleep(80);
                    }
                    Thread.Sleep(60);
                }
            }
        }

        private void Colar_Click(object sender, RoutedEventArgs e)
        {
            LimparErro();
            PararCamera();
            if (!Clipboard.ContainsImage())
            {
                Erro("Não há imagem na área de transferência.");
                return;
            }
            try
            {
                var img = Clipboard.GetImage();
                preview.Source = img;
                textoPreview.Visibility = Visibility.Collapsed;
                Aceitar(LeitorQr.DeImagem(img));
            }
            catch (Exception ex)
            {
                Erro(ex.Message);
            }
        }

        private void Arquivo_Click(object sender, RoutedEventArgs e)
        {
            LimparErro();
            PararCamera();
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Imagem ou texto|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.txt|Todos os arquivos|*.*"
            };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var ext = Path.GetExtension(dlg.FileName) ?? string.Empty;
                if (ext.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    preview.Source = null;
                    textoPreview.Visibility = Visibility.Visible;
                    textoPreview.Text = Path.GetFileName(dlg.FileName);
                    Aceitar(LeitorQr.DeTexto(File.ReadAllText(dlg.FileName)));
                    return;
                }

                BitmapSource img;
                using (var fs = File.OpenRead(dlg.FileName))
                {
                    var frame = BitmapFrame.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    img = frame;
                }
                preview.Source = img;
                textoPreview.Visibility = Visibility.Collapsed;
                var uris = LeitorQr.DeImagem(img);
                if (uris.Count == 0)
                    uris = LeitorQr.DeTexto(File.ReadAllText(dlg.FileName));
                Aceitar(uris);
            }
            catch (Exception)
            {
                try
                {
                    Aceitar(LeitorQr.DeTexto(File.ReadAllText(dlg.FileName)));
                }
                catch (Exception ex)
                {
                    Erro(ex.Message);
                }
            }
        }

        private void Enviar_Click(object sender, RoutedEventArgs e)
        {
            if (_uris.Count == 0 || Interlocked.Exchange(ref _enviando, 1) == 1) return;
            botaoEnviar.IsEnabled = false;
            botaoCamera.IsEnabled = false;
            botaoColar.IsEnabled = false;
            botaoArquivo.IsEnabled = false;
            textoErro.Text = string.Empty;
            textoPreview.Text = "Enviando para o Ente…";
            var uris = _uris.ToArray();
            System.Threading.Tasks.Task.Run(() => new EnteAuthService().Cadastrar(uris))
                .ContinueWith(t =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _enviando = 0;
                        botaoCamera.IsEnabled = true;
                        botaoColar.IsEnabled = true;
                        botaoArquivo.IsEnabled = true;
                        if (t.IsFaulted)
                        {
                            botaoEnviar.IsEnabled = true;
                            Erro(t.Exception?.GetBaseException().Message ?? "Falha ao enviar.");
                            return;
                        }
                        var r = t.Result;
                        if (!r.Sucesso)
                        {
                            botaoEnviar.IsEnabled = true;
                            Erro(r.Mensagem);
                            return;
                        }
                        DialogResult = true;
                    });
                });
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Aceitar(List<string> uris)
        {
            _uris.Clear();
            var rotulos = new List<string>();
            if (uris != null)
            {
                foreach (var uri in uris)
                {
                    if (!ParserOtpAuth.Tentar(uri, out var entrada)) continue;
                    _uris.Add(LeitorQr.Montar(entrada));
                    rotulos.Add(entrada.Rotulo);
                }
            }
            listaChaves.ItemsSource = rotulos.ToArray();
            botaoEnviar.IsEnabled = _uris.Count > 0;
            if (_uris.Count == 0)
            {
                Erro("Não achei um otpauth://totp.");
                return;
            }
            textoErro.Foreground = (System.Windows.Media.Brush)FindResource("CorTextoFraco");
            textoErro.Text = _uris.Count == 1
                ? "1 chave pronta para enviar."
                : _uris.Count + " chaves prontas para enviar.";
        }

        private static List<string> DecodificarMat(Mat mat)
        {
            if (mat == null || mat.Empty()) return new List<string>();
            Mat proprio = null;
            var src = mat;
            if (!mat.IsContinuous())
            {
                proprio = mat.Clone();
                src = proprio;
            }
            try
            {
                using (var bgra = new Mat())
                {
                    Cv2.CvtColor(src, bgra, ColorConversionCodes.BGR2BGRA);
                    var w = bgra.Width;
                    var h = bgra.Height;
                    var step = (int)bgra.Step();
                    var pixels = new byte[w * h * 4];
                    if (step == w * 4)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(bgra.Data, pixels, 0, pixels.Length);
                    }
                    else
                    {
                        for (var y = 0; y < h; y++)
                            System.Runtime.InteropServices.Marshal.Copy(bgra.Data + y * step, pixels, y * w * 4, w * 4);
                    }
                    return LeitorQr.DePixels(pixels, w, h, RGBLuminanceSource.BitmapFormat.BGRA32);
                }
            }
            finally
            {
                proprio?.Dispose();
            }
        }

        private void MostrarMat(Mat mat)
        {
            try
            {
                Cv2.ImEncode(".bmp", mat, out var bytes);
                var img = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = ms;
                    img.EndInit();
                }
                img.Freeze();
                preview.Source = img;
                textoPreview.Visibility = Visibility.Collapsed;
            }
            catch { }
            finally
            {
                mat.Dispose();
            }
        }

        private void PararCamera()
        {
            _cameraViva = false;
            var cam = _camera;
            _camera = null;
            if (cam == null) return;
            try { cam.Release(); } catch { }
            try { cam.Dispose(); } catch { }
        }

        private void LimparErro() => textoErro.Text = string.Empty;

        private void Erro(string msg)
        {
            textoErro.Foreground = (System.Windows.Media.Brush)FindResource("CorPerigo");
            textoErro.Text = msg ?? string.Empty;
        }
    }
}

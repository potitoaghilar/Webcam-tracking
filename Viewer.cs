using System;
using System.Windows;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Video.VFW;
using AForge.Imaging;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;

namespace Viewer
{
    public partial class Viewer : Form
    {

        bool GLloaded = false;

        Dictionary<string, string> datas = new Dictionary<string, string>();
        FilterInfoCollection inputDevices = null;
        VideoCaptureDevice webcam = null;

        int currFrame = 0;
        bool firstFrame = false;
        Bitmap startGame, currFrameBitmap;

        Rectangle[] cardsRects = new Rectangle[]{};
        List<string> cardsOnTable = new List<string>();

        public Viewer()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!webcam.IsRunning)
            {
                backgroundReader.RunWorkerAsync();
                button1.Enabled = false;
                comboBox1.Enabled = false;
                button1.Text = "Avvio... (4)";
                webcam.NewFrame += new NewFrameEventHandler(newFrame);
                webcam.Start();
                timer1.Start();
            }
            else
            {
                webcam.Stop();
                backgroundReader.CancelAsync();
                comboBox1.Enabled = true;
                //pictureBox1.Image = null;
                startGame = null;
                currFrameBitmap = null;
                firstFrameGL = true;
                webcam.NewFrame -= new NewFrameEventHandler(newFrame);
                glView.Invalidate();
                currFrame = 0;
                button1.Text = "Avvia acquisizione";
            }
        }

        void newFrame(object sender, NewFrameEventArgs e)
        {
            currFrame++;
            if (currFrameBitmap != null) currFrameBitmap.Dispose();
            if (firstFrame) { startGame = (Bitmap)e.Frame.Clone(); firstFrame = false; }
            if (currFrame % 3 == 0)
            {
                //if (startGame != null) pictureBox1.Image = diff(startGame, (Bitmap)e.Frame.Clone());
                //else pictureBox1.Image = (Bitmap)e.Frame.Clone();

                currFrameBitmap = (Bitmap)e.Frame.Clone();
                glView.Invalidate();
            }
            if (currFrame == 1000) currFrame = 0;
            e.Frame.Dispose();
        }

        Bitmap diff(Bitmap prev, Bitmap curr)
        {
            unsafe
            {
                Bitmap diffBitmap = new Bitmap(prev.Width, prev.Height), finalBitmap = new Bitmap(prev.Width, prev.Height);

                BitmapData prevData = prev.LockBits(new Rectangle(0, 0, prev.Width, prev.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                BitmapData currData = curr.LockBits(new Rectangle(0, 0, prev.Width, prev.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                BitmapData diffData = diffBitmap.LockBits(new Rectangle(0, 0, prev.Width, prev.Height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                BitmapData finalData = finalBitmap.LockBits(new Rectangle(0, 0, prev.Width, prev.Height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                int pixelSize = 3;

                for (int y = 0; y < prev.Height; y++)
                {
                    byte* prevRow = (byte*)prevData.Scan0 + (y * prevData.Stride);
                    byte* currRow = (byte*)currData.Scan0 + (y * currData.Stride);
                    byte* diffRow = (byte*)diffData.Scan0 + (y * diffData.Stride);

                    for (int x = 0; x < prev.Width; x++)
                    {
                        byte diffByte = (byte)Math.Min(Math.Max(Math.Abs((currRow[x * pixelSize] + currRow[x * pixelSize + 1] + currRow[x * pixelSize + 2] / 3) - (prevRow[x * pixelSize] + prevRow[x * pixelSize + 1] + prevRow[x * pixelSize + 2] / 3)), 0), 255);
                        if (diffByte < 128) diffByte = 0; else diffByte = 255;
                        diffRow[x * pixelSize] = diffByte;
                        diffRow[x * pixelSize + 1] = diffByte;
                        diffRow[x * pixelSize + 2] = diffByte;
                    }
                }

                prev.UnlockBits(prevData);

                for (int y = 0; y < prev.Height; y++)
                {
                    byte* currRow = (byte*)currData.Scan0 + (y * currData.Stride);
                    byte* diffRow = (byte*)diffData.Scan0 + (y * diffData.Stride);
                    byte* finalRow = (byte*)finalData.Scan0 + (y * finalData.Stride);

                    for (int x = 0; x < prev.Width; x++)
                    {
                        byte finalByteR, finalByteG, finalByteB;
                        if (diffRow[x * pixelSize] == 255) {
                            finalByteR = (byte)currRow[x * pixelSize];
                            finalByteG = (byte)currRow[x * pixelSize + 1];
                            finalByteB = (byte)currRow[x * pixelSize + 2];
                        }
                        else { finalByteR = 0; finalByteG = 0; finalByteB = 0; }
                        finalRow[x * pixelSize] = finalByteR;
                        finalRow[x * pixelSize + 1] = finalByteG;
                        finalRow[x * pixelSize + 2] = finalByteB;
                    }
                }

                diffBitmap.UnlockBits(diffData);
                curr.UnlockBits(currData);
                finalBitmap.UnlockBits(finalData);

                return finalBitmap;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (webcam.IsRunning) {
                webcam.Stop();
                backgroundReader.CancelAsync();
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            webcam = new VideoCaptureDevice(datas[comboBox1.Text]);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            inputDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo device in inputDevices)
            {
                comboBox1.Items.Add(device.Name);
                datas.Add(device.Name, device.MonikerString);
            }
            if (comboBox1.Items.Count == 0) button1.Enabled = false; else comboBox1.SelectedIndex = 0;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (button1.Text == "Avvio... (1)")
            {
                button1.Text = "Ferma acquisizione";
                button1.Enabled = true;
                timer1.Stop();
                firstFrame = true;
            }
            else button1.Text = "Avvio... (" + (int.Parse(button1.Text.Substring(10, 1)) - 1) + ")";
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (cardsRects.Length > 0) e.Graphics.DrawRectangles(new Pen(Color.Red, 2), cardsRects);
        }

        private void backgroundReader_DoWork(object sender, DoWorkEventArgs e)
        {
            if (startGame != null)
            { 
                //TODO
            }
        }

        int blankTexId, firstFrameTexId, currFrameTexId, shaderProgNormal, shaderProgDiff, fragNormal, fragDiff, finalTexId;
        bool firstFrameGL = true;
        Bitmap finalTex;

        private void glView_Load(object sender, EventArgs e)
        {
            GLloaded = true;
            setViewport();

            GL.Enable(EnableCap.Texture2D);
            GL.GenTextures(1, out firstFrameTexId); GL.GenTextures(1, out currFrameTexId); GL.GenTextures(1, out finalTexId);
            shaderProgNormal = GL.CreateProgram(); shaderProgDiff = GL.CreateProgram();
            fragNormal = GL.CreateShader(ShaderType.FragmentShader); fragDiff = GL.CreateShader(ShaderType.FragmentShader);

            String fragNormalSource = @"
uniform sampler2D currFrameTex;

void main(void)
{
    gl_FragColor = texture2D(currFrameTex, gl_TexCoord[0].st);
}";

            String fragDiffSource = @"
uniform sampler2D currFrameTex;
uniform sampler2D firstFrameTex;

void main(void)
{
    vec4 currFrameTexChannels = texture2D(currFrameTex, gl_TexCoord[0].st);
    vec4 firstFrameTexChanels = texture2D(firstFrameTex, gl_TexCoord[0].st);
    float firstCol = (firstFrameTexChanels.r + firstFrameTexChanels.g + firstFrameTexChanels.b) / 3;
    float currCol = (currFrameTexChannels.r + currFrameTexChannels.g + currFrameTexChannels.b) / 3;
    float diff = abs(currCol - firstCol);
    if(diff < .2) diff = 0; else diff = 1;
    vec3 finalColor = lerp(vec3(0,0,0), currFrameTexChannels.rgb, diff);
    gl_FragColor = vec4(finalColor, 1);
}";

            GL.ShaderSource(fragNormal, fragNormalSource); GL.CompileShader(fragNormal);
            GL.ShaderSource(fragDiff, fragDiffSource); GL.CompileShader(fragDiff);
            GL.AttachShader(shaderProgNormal, fragNormal); GL.AttachShader(shaderProgDiff, fragDiff);
            GL.LinkProgram(shaderProgNormal); GL.LinkProgram(shaderProgDiff);

            GL.ClearColor(.94f, .94f, .94f, 1);
        }

        void setViewport()
        {
            int w = glView.Width;
            int h = glView.Height;
            GL.Viewport(0, 0, w, h);
            //Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView((float)Math.PI / 3, 4/3, .3f, 1000);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, w, 0, h, -1, 1);
            //GL.LoadMatrix(ref projection);
            //Matrix4 modelview = Matrix4.LookAt(new Vector3(0, 0, -9), Vector3.UnitZ, Vector3.UnitY);
            GL.MatrixMode(MatrixMode.Modelview);
            //GL.LoadMatrix(ref modelview);
            //GL.Rotate(5, Vector3d.UnitY);
        }
        
        private void glView_Paint(object sender, PaintEventArgs e)
        {
            Render();
            if (startGame != null)
            {
                BlobCounter blobCounter = new BlobCounter();
                blobCounter.MinWidth = 50; blobCounter.MinHeight = 50;
                blobCounter.FilterBlobs = true;
                blobCounter.ObjectsOrder = ObjectsOrder.Size;
                blobCounter.ProcessImage(finalTex);
                cardsRects = blobCounter.GetObjectsRectangles();
            }
            pictureBox1.Image = finalTex;
        }
        void Render()
        {
            if (!GLloaded) return;
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (currFrameBitmap != null)
            {
                loadTexture(currFrameTexId, currFrameBitmap, true);
                if (startGame != null)
                {
                    if (firstFrameGL) { loadTexture(firstFrameTexId, startGame, false); firstFrameGL = false; }
                    loadTexture(finalTexId, finalTex, false);
                    GL.ActiveTexture(TextureUnit.Texture1);
                    GL.BindTexture(TextureTarget.Texture2D, firstFrameTexId);
                    GL.Uniform1(GL.GetUniformLocation(shaderProgDiff, "firstFrameTex"), 1);
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, currFrameTexId);
                    GL.Uniform1(GL.GetUniformLocation(shaderProgDiff, "currFrameTex"), 0);
                    GL.UseProgram(shaderProgDiff);
                }
                else
                {
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, currFrameTexId);
                    GL.Uniform1(GL.GetUniformLocation(shaderProgNormal, "currFrameTex"), 0);
                    GL.UseProgram(shaderProgNormal);
                }
            }
            else
            {
                GL.UseProgram(0);
                Bitmap blank = new Bitmap(1, 1); blank.SetPixel(0, 0, Color.White);
                loadTexture(blankTexId, blank, true);
            }

            GL.Begin(PrimitiveType.Polygon);
            GL.TexCoord2(0, 1);
            GL.Vertex2(0, 0);
            GL.TexCoord2(1, 1);
            GL.Vertex2(glView.Width, 0);
            GL.TexCoord2(1, 0);
            GL.Vertex2(glView.Width, glView.Height);
            GL.TexCoord2(0, 0);
            GL.Vertex2(0, glView.Height);
            GL.End();

            getView();

            glView.SwapBuffers();
        }

        void loadTexture(int id, Bitmap tex, bool disposeAfterLoad, int prevTexId = -1)
        {
            GL.BindTexture(TextureTarget.Texture2D, id);
            BitmapData texData = tex.LockBits(new Rectangle(0, 0, tex.Width, tex.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb, tex.Width, tex.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, texData.Scan0);
            tex.UnlockBits(texData);
            if (disposeAfterLoad) tex.Dispose();
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)TextureMagFilter.Linear);
            if (prevTexId != -1) GL.BindTexture(TextureTarget.Texture2D, prevTexId);
        }

        void getView()
        {
            GL.DrawBuffer(DrawBufferMode.Back);
            GL.ReadBuffer(ReadBufferMode.Back);
            if (finalTex != null) finalTex.Dispose();
            finalTex = new Bitmap(glView.Width, glView.Height);
            BitmapData finalTexData = finalTex.LockBits(glView.ClientRectangle, ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            GL.Finish();
            GL.ReadPixels(0, 0, glView.Width, glView.Height, OpenTK.Graphics.OpenGL.PixelFormat.Bgr, PixelType.UnsignedByte, finalTexData.Scan0);
            finalTex.UnlockBits(finalTexData);
            finalTex.RotateFlip(RotateFlipType.RotateNoneFlipY);
        }

        private void glView_Resize(object sender, EventArgs e) { setViewport(); }
    }
}

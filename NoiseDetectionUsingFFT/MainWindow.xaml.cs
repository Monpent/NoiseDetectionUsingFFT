using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Win32;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;

namespace NoiseDetectionUsingFFT
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            soundOutput = false;
            tableOutput = false;
            graphOutput = false;
            pitchDetection = false;
            noiseDetection = false;
            subfixTextboxChange = false;
            folderNameTextboxChange = false;
            singleFileOperation = true;
            errorOfinput = false;
            skipSevenF = true;
            IstTime = true;
            wavFileName = null;
            folderLoc = null;
            subfix = null;
            removeTypeCode = 0;
            oritime = 0;
            currTime = 0;
            removeRatio = 0;
            processedTime = 0;
            highestFrequcncy = 0;
            newHighestFrequcncy = 0;
            newSecondHighestFrequency = 0;
            player = new MediaPlayer();
        }

        //----------------  data from interface  -------------------
        private int removeTypeCode;
        private bool folderNameTextboxChange;
        private bool subfixTextboxChange;
        private bool noiseDetection;
        private bool pitchDetection;
        private bool graphOutput;
        private bool tableOutput;
        private bool soundOutput;
        private bool skipSevenF;
        private string folderLoc;
        private string subfix;
        private string fileOrFolder;
        private bool singleFileOperation;
        //----------------  data for excuation  -------------------
        private byte[] wav_encode;
        private bool errorOfinput;
        private ArrayList fileName;
        private int wavFileCount;
        private double oritime;
        private double processedTime;
        private float removeRatio;
        private double highestFrequcncy;
        private double secondHighestFrequency;
        private double newHighestFrequcncy;
        private double newSecondHighestFrequency;
        private int fileLength;
        private int Fs;
        private byte[] data_r;
        private byte[] freq;
        private double fm;
        private int refreshCounter;
        private float currTime;
        private float secondTime;
        private bool IstTime;
        private string wavFileName;
        private MediaPlayer player;

        /* -----------------------------------------------------------------------------------------------------------
         * -----------------------------------   checking for inputs   -----------------------------------------------
         * -----------------------------------------------------------------------------------------------------------
         *                          contain error  =>    pop window for error    
         *                                  not    ==>        
         *                                          is folder   =>   execuate readFile 
         *                                          is FileName =>   execuate getAudioInfo      
         */
        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            slider.Value = 0;
            currentTime.Text = "0";
            secondSlider.Value = 0;
            secondTimeTextBox.Text = "0";
            sectionFFTbox.Source = null;
            playButton.IsEnabled = true;

            if (folderNameTextboxChange == true)
            {
                folderLoc = folderName.Text;
            }
            else
                errorOfinput = true;

            if (subfixTextboxChange == true)
            {
                subfix = fileSubfix.Text;
            }
            else
                errorOfinput = true;

            wavFileCount = 0;

            if (errorOfinput == true)
            {
                DialogResult stupid = System.Windows.Forms.DialogResult.No;
                do
                {
                    stupid = System.Windows.Forms.MessageBox.Show("Input Error: 你傻吗你傻吗你傻吗\nError@line127", "YOU STUPID", MessageBoxButtons.YesNo);
                } while (stupid != System.Windows.Forms.DialogResult.Yes);
                errorOfinput = false;
            }
            else
            {
                if (fileOrFolder == "Folder")
                {
                    DirectoryInfo TheFolder = new DirectoryInfo(folderLoc);
                    readFile(TheFolder);
                }
                else
                {
                    getAudioInfo(folderLoc);
                }
            }
        }

        /* -----------------------------------------------------------------------------------------------------------
         * ------------------------ read file from the first time  ---------------------------------------------------
         * -----------------------------------------------------------------------------------------------------------    
         */
        private void readFile(FileSystemInfo TheFolder)
        {
            /*         This mthod will end up with a global variable  
             *                                     All wave file that in the folder  --  arrayList fileName
             */

            fileName = new ArrayList();
            if (!TheFolder.Exists) return;
            DirectoryInfo dir = TheFolder as DirectoryInfo;
            if (dir == null) return;

            FileSystemInfo[] files = dir.GetFileSystemInfos();
            for (int i = 0; i < files.Length; i++)
            {
                FileInfo file = files[i] as FileInfo;
                if (file != null)
                {
                    if (file.Extension == ".wav")
                    {
                        if (singleFileOperation == false)
                        {
                            getAudioInfo(files[i].FullName);
                        }
                        else
                        {
                            fileName.Add(files[i].FullName);
                        }

                    }

                }
                else
                {
                    readFile(files[i]);
                }
            }

            if (singleFileOperation == true)
                getAudioInfo((string)fileName[wavFileCount]);

            return;
        }
        private void getAudioInfo(string waveFileName)
        {
            /* Read the audio file
             * The following data needed: 
             *          byteLength    meaning 
             *          4               check if riff (wav file)
             *          4               get length of file      size - 308 (skip the 1st 300 7F)
             *                  skip next 20 bytes
             *          4               sample rate Fs  8000
             *                  skip next 4 bytes
             *          2               get bit info
             *                  skip next 10 bytes          
             *                  actual data            
             */
            wavFileName = waveFileName;
            refreshCounter = 0;
            statusFilePath.Text = waveFileName;
            System.IO.FileStream WaveFile = System.IO.File.OpenRead(waveFileName);
            BinaryReader bread = new BinaryReader(WaveFile);
            byte[] riff = new byte[4];
            byte[] riffSize = new byte[4];
            byte[] sample = new byte[4];
            byte[] bits = new byte[2];
            double[] result_r = new double[1024];
            double[] result_i = new double[1024];
            double[] data_i = new double[1024];
            double[] tempData = new double[1024];
            riff = bread.ReadBytes(4);
            riffSize = bread.ReadBytes(4);
            bread.ReadBytes(20);
            sample = bread.ReadBytes(4);

            // Data length
            fileLength = 0x1000000 * riffSize[3] + 0x10000 * riffSize[2] + 0x100 * riffSize[1] + riffSize[0];

            // Fs
            Fs = 0x1000000 * sample[3] + 0x10000 * sample[2] + 0x100 * sample[1] + sample[0];
            bread.ReadBytes(2);

            //bit info
            bits = bread.ReadBytes(2);
            if (bits[1] != 0x00 || bits[0] != 0x08)
            {
                System.Windows.Forms.MessageBox.Show("Wrong waveform! \nError@line235");
                return;
            }
            bread.ReadBytes(10);

            //original time
            oritime = fileLength * 1.0 / Fs;
            if (skipSevenF == true)
            {
                bread.ReadBytes(300);
                fileLength = fileLength - 300;
            }
            statusTimeTextBlock.Text = oritime.ToString();

            InitEncode();
            //read data and remove zeros
            data_r = new byte[fileLength];
            data_r = bread.ReadBytes(fileLength);
            for (int i = 0; i < data_r.Length - 1; i++)
            {
                data_r[i] = wav_encode[data_r[i]];
            }
            remove0Audio(ref data_r);

            fileLength = data_r.Length;
            processedTime = fileLength * 1.0 / Fs;
            ProcessedTimeTextBlock.Text = processedTime.ToString();
            removeRatio = (float)(1 - processedTime / oritime);
            RemoveRatioTextBlock.Text = removeRatio.ToString();

            // time after remove zeros and time
            double removedTime = fileLength * 1.0 / Fs;
            int segment = fileLength / 1024;
            int len = segment * 1024;

            //郭
            processedTime = len * 1.0 / Fs;
            ProcessedTimeTextBlock.Text = processedTime.ToString();

            //pass the data and do FFT
            double[] totalResult_r = new double[len];
            double[] totalResult_i = new double[len];
            for (int i = 0; i < segment - 1; i++)
            {
                Array.Copy(data_r, i * 1024, tempData, 0, 1024);
                Dit2_FFT(ref tempData, ref data_i, ref result_r, ref result_i);
                Array.Copy(result_r, 0, totalResult_r, i * 1024, 1024);
                Array.Copy(result_i, 0, totalResult_i, i * 1024, 1024);
            }
            bread.Close();

            fm = 3000 * len / Fs;

            //逄don't touch, 1  -- very important
            //double[] f = new double[(int)fm+1];
            double[] frequency = new double[(int)fm + 1];

            //郭
            double[] frequency_r = new double[(int)fm + 1];
            double[] frequency_i = new double[(int)fm + 1];

            freq = new byte[512];
            double[] frequen = new double[1024];

            //for (int i = 0; i < 1024; i++ )
            //{
            //    frequen[i] = 0;
            //    frequency_r[i] = 0;
            //    frequency_i[i] = 0;
            //}

            //for (int i = 0; i < fm; i++)
            //{
            //    //f[i] = i * Fs / len;
            //    frequency[i] = Math.Sqrt(totalResult_r[i] * totalResult_r[i] + totalResult_i[i] * totalResult_i[i]);
            //}

            for (int i = 0; i < fm; i++)
            {
                frequency_r[i % 1024] += totalResult_r[i];
                frequency_i[i % 1024] += totalResult_i[i];
            }

            for (int i = 0; i < 1024; i++)
            {
                frequency_r[i] = frequency_r[i] / segment;
                frequency_i[i] = frequency_i[i] / segment;
                frequency[i] = Math.Sqrt(frequency_r[i] * frequency_r[i] + frequency_i[i] * frequency_i[i]);
            }
            frequency[0] = 0;
            //for (int i = 0; i < segment; i++)
            //{
            //    frequen[i % 1024] += frequency[i];
            //    if (frequency[i] > highestFrequcncy)
            //        highestFrequcncy = frequency[i];
            //}

            for (int i = 0; i < 512; i++)
            {
                //freq[i] = (byte)(frequen[i] * 0x7f / segment / highestFrequcncy);
                freq[i] = (byte)(frequency[i]);
            }

            //System.Windows.MessageBox.Show("xxx");
            //Array.Copy(frequen, freq, 512);

            highestFrequcncy = 0;
            secondHighestFrequency = 0;
            for (int i = 0; i < 512; i++)
            {
                if (highestFrequcncy < freq[i])
                {
                    secondHighestFrequency = highestFrequcncy;
                    highestFrequcncy = freq[i];
                }
                else
                {
                    if (secondHighestFrequency < freq[i])
                    {
                        secondHighestFrequency = freq[i];
                    }
                }
            }
            secndPeakTextBlock.Text = secondHighestFrequency.ToString();
            peakValueTextBlock.Text = highestFrequcncy.ToString();

            savewavJPGfile();
            saveFFTJPGfile(0);
            saveCSVfile();

            //if (wavFileName == null)
            //{
            //    System.Windows.MessageBox.Show("Select a sound first! Error@line1085");
            //    return;
            //}
            player.Open(new Uri(wavFileName, UriKind.Relative));
            //mediaTimeLineStoryBoard.Source = new Uri(wavFileName, UriKind.Relative);
        }


        /* -----------------------------------------------------------------------------------------------------------
         * -------------------  save file into current folder and display data  --------------------------------------
         * -----------------------------------------------------------------------------------------------------------
         * P.S. the result of execuation will be displayed in the statusbar    
         * 
         * IOZY == 1024
         */
        private void refreshIOZYFFT()
        {
            double FFTpoint = (currTime - 0.0625) * Fs;
            double[] data_i = new double[1024];
            double[] tempData = new double[1024];
            double[] result_r = new double[1024];
            double[] result_i = new double[1024];
            if (currTime < 0.06025 && oritime - currTime > 0.125)
            {
                FFTpoint = currTime * Fs;
            }
            if (currTime >= 0.125 && (oritime - currTime) < 0.15)
            {
                FFTpoint = oritime * Fs - 2048;
            }
            string waveFileName;
            Array.Copy(data_r, (int)FFTpoint, tempData, 0, 1024);
            Dit2_FFT(ref tempData, ref data_i, ref result_r, ref result_i);
            double[] frequency = new double[1025];
            freq = new byte[512];
            byte[] frequen = new byte[1025];
            for (int i = 0; i < 1024; i++)
            {
                frequency[i] = Math.Sqrt(result_r[i] * result_r[i] + result_i[i] * result_i[i]);
                frequen[i] = (byte)frequency[i];
            }
            frequen[0] = 0;
            Array.Copy(frequen, freq, 512);
            highestFrequcncy = 0;
            secondHighestFrequency = 0;
            for (int i = 0; i < 512; i++)
            {
                if (highestFrequcncy < freq[i])
                {
                    secondHighestFrequency = highestFrequcncy;
                    highestFrequcncy = freq[i] * 1.0;
                }
                else
                {
                    if (secondHighestFrequency < freq[i])
                    {
                        secondHighestFrequency = freq[i] * 1.0;
                    }
                }
            }
            secndPeakTextBlock.Text = secondHighestFrequency.ToString();
            peakValueTextBlock.Text = highestFrequcncy.ToString();
            if (fileOrFolder == "folder")
            {
                waveFileName = (string)fileName[wavFileCount];
                saveFFTJPGfile(0);
            }
            else
            {
                saveFFTJPGfile(0);
            }
        }
        private void refreshSegmentFFT()
        {
            int FFTpointStart = (int)(currTime * Fs);
            int FFTpointEnd = (int)(secondTime * Fs);
            int totalFFTpoint = FFTpointEnd - FFTpointStart;
            if (totalFFTpoint < 0)
            {
                int temp = FFTpointEnd;
                FFTpointEnd = FFTpointStart;
                FFTpointStart = temp;
                totalFFTpoint = Math.Abs(totalFFTpoint);
            }
            if (totalFFTpoint < 1024)
            {
                sectionFFTbox.Source = null;
                System.Windows.MessageBox.Show(totalFFTpoint.ToString());
                return;
            }
            if (totalFFTpoint < 2048)
            {
                currTime = (FFTpointEnd + FFTpointStart) / 2;
                refreshIOZYFFT();
                sectionFFTbox.Source = null;
                return;
            }
            double[] data_i = new double[1024];
            double[] tempData = new double[1024];
            double[] result_r = new double[1024];
            double[] result_i = new double[1024];
            string waveFileName;
            //Array.Copy(data_r, (int)FFTpointStart, tempData, 0, 1024);
            int segment = totalFFTpoint / 1024;
            if (segment < 1)
                segment = 1;
            int len = segment * 1024;

            //pass the data and do FFT
            double[] totalResult_r = new double[len];
            double[] totalResult_i = new double[len];
            for (int i = 0; i < segment - 1; i++)
            {
                Array.Copy(data_r, FFTpointStart + i * 1024, tempData, 0, 1024);
                Dit2_FFT(ref tempData, ref data_i, ref result_r, ref result_i);
                Array.Copy(result_r, 0, totalResult_r, i * 1024, 1024);
                Array.Copy(result_i, 0, totalResult_i, i * 1024, 1024);
            }
            double[] frequency = new double[len + 1];
            double[] frequency_r = new double[len + 1];
            double[] frequency_i = new double[len + 1];

            freq = new byte[512];
            double[] frequen = new double[1024];

            for (int i = 0; i < len; i++)
            {
                frequency_r[i % 1024] += totalResult_r[i];
                frequency_i[i % 1024] += totalResult_i[i];
            }

            for (int i = 0; i < 1024; i++)
            {
                frequency_r[i] = frequency_r[i] / segment;
                frequency_i[i] = frequency_i[i] / segment;
                frequency[i] = Math.Sqrt(frequency_r[i] * frequency_r[i] + frequency_i[i] * frequency_i[i]);
            }
            frequency[0] = 0;
            for (int i = 0; i < 512; i++)
            {
                freq[i] = (byte)(frequency[i]);
            }
            newHighestFrequcncy = 0;
            newSecondHighestFrequency = 0;
            for (int i = 0; i < 512; i++)
            {
                if (newHighestFrequcncy < freq[i])
                {
                    newSecondHighestFrequency = newHighestFrequcncy;
                    newHighestFrequcncy = freq[i];
                }
                else
                {
                    if (newSecondHighestFrequency < freq[i])
                    {
                        newSecondHighestFrequency = freq[i];
                    }
                }
            }
            secondSecndPeakTextBlock.Text = newSecondHighestFrequency.ToString();
            secondPeakValueTextBlock.Text = newHighestFrequcncy.ToString();
            if (fileOrFolder == "folder")
            {
                waveFileName = (string)fileName[wavFileCount];
                saveFFTJPGfile(1);
            }
            else
            {
                saveFFTJPGfile(1);
            }
        }
        private void saveCSVfile()
        {
            //get the file name
            int s = 0;
            string newfolderLoc = folderLoc;
            if (fileOrFolder == "File")
            {
                s = folderLoc.LastIndexOf("\\");
                newfolderLoc = folderLoc.Substring(0, s);
            }
            string excelfilename;
            if (refreshCounter == 0)
                excelfilename = newfolderLoc + "\\" + subfix + "_fftpoints.csv";
            else
                excelfilename = newfolderLoc + "\\" + subfix + "_fftpoints.csv";

            //write the file
            bool makeNewFile = false;
            if (File.Exists(excelfilename) && IstTime == true)
            {
                DialogResult dr = System.Windows.Forms.MessageBox.Show("The subfix has been used.\nDo you wish to replace the file?\nReplace Click 'Yes'\nWrite on the new line click 'No'",
                    "Notice", MessageBoxButtons.YesNo);
                if (dr == System.Windows.Forms.DialogResult.Yes)
                {
                    makeNewFile = true;
                }
                IstTime = false;
            }
            if ((makeNewFile || !File.Exists(excelfilename)) && IstTime == false)
            {
                FileStream newfs = new FileStream(excelfilename, FileMode.Create);
                StreamWriter newsw = new StreamWriter(newfs);
                newsw.Write("File Directory \tFFT Times \tFFT@ Time \tSecond FFT@ Time \tInitial Time \tTime After Process \tRemove Time Ratio \tHighest Frequency \tSecond Highest Frequency \tGraph 2 Highest Frequency \tGraph 2 Second Highest Frequency \tConclusion\n");
                newsw.Flush();
                newsw.Close();
                newfs.Close();
            }
            FileStream fs = new FileStream(excelfilename, FileMode.Append);
            StreamWriter sw = new StreamWriter(fs);
            string fileInfo;
            fileInfo = wavFileName + "\t" + refreshCounter.ToString() + "\t" + currTime.ToString() + "\t" + secondTime.ToString() + "\t" + oritime.ToString() + "\t" + processedTime.ToString() + "\t";
            fileInfo += removeRatio.ToString() + "\t" + highestFrequcncy.ToString() + "\t" + secondHighestFrequency.ToString() + "\t";
            fileInfo += newHighestFrequcncy.ToString() + "\t" + newSecondHighestFrequency.ToString() + "\t" + "N\\A" + "\n";
            sw.Write(fileInfo);
            sw.Flush();
            sw.Close();
            fs.Close();
        }
        private void saveFFTJPGfile(int sectionCode)
        {
            //print the fft into the fftBox
            string fftLocAndName;
            int s = 0;
            s = wavFileName.LastIndexOf(".");
            System.Drawing.Image fftImage = GetWaveFormImage(false, 150, 75,
                System.Drawing.Color.Black, System.Drawing.Color.Lime, freq);  // Lime and LimeGreen

            fftLocAndName = wavFileName.Substring(0, s);
            if (sectionCode == 0)
            {
                if (refreshCounter == 0)
                    fftLocAndName = fftLocAndName + "_" + subfix + "_1024fft" + ".jpg";
                else
                    fftLocAndName = fftLocAndName + "_" + subfix + currTime.ToString() + "_1024fft" + ".jpg";
            }
            else
            {
                if (refreshCounter == 0)
                    fftLocAndName = fftLocAndName + "_" + subfix + "_sectionfft" + ".jpg";
                else
                    fftLocAndName = fftLocAndName + "_" + subfix + currTime.ToString() + "_sectionfft" + ".jpg";
            }

            if (File.Exists(fftLocAndName))
            {
                File.Delete(fftLocAndName);
            }

            fftImage.Save(fftLocAndName);

            // Read byte[] from png file
            BinaryReader binReader = new BinaryReader(File.Open(fftLocAndName, FileMode.Open));
            FileInfo fileInfo = new FileInfo(fftLocAndName);
            byte[] bytes = binReader.ReadBytes((int)fileInfo.Length);
            binReader.Close();

            // Init bitmap
            BitmapImage fftBitmap = new BitmapImage();
            fftBitmap.BeginInit();
            fftBitmap.StreamSource = new MemoryStream(bytes);
            fftBitmap.EndInit();
            //display jpg and change status
            if (sectionCode == 0)
            {
                fftBox.Source = fftBitmap;
                statusLatFFT.Text = "Last FFT Image Saved";
            }
            else
            {
                sectionFFTbox.Source = fftBitmap;
                secondStatusLatFFT.Text = "Last FFT Image Saved";
            }
        }
        private void savewavJPGfile()
        {
            //print original audio 
            System.Drawing.Image origAudioImage = GetWaveFormImage(true, 512, 258, System.Drawing.Color.Black, System.Drawing.Color.Lime, data_r);  // Lime and LimeGreen
            string oriWaveLocAndName;
            int s = 0;
            s = wavFileName.LastIndexOf(".");
            oriWaveLocAndName = wavFileName.Substring(0, s);
            oriWaveLocAndName = oriWaveLocAndName + "_" + subfix + ".jpg";
            if (File.Exists(oriWaveLocAndName))
            {
                File.Delete(oriWaveLocAndName);
            }

            origAudioImage.Save(oriWaveLocAndName);
            // Read byte[] from png file
            BinaryReader binReader = new BinaryReader(File.Open(oriWaveLocAndName, FileMode.Open));
            FileInfo fileInfo = new FileInfo(oriWaveLocAndName);
            byte[] bytes = binReader.ReadBytes((int)fileInfo.Length);
            binReader.Close();

            // Init bitmap
            BitmapImage origAudioBitmap = new BitmapImage();
            origAudioBitmap.BeginInit();
            origAudioBitmap.StreamSource = new MemoryStream(bytes);
            origAudioBitmap.EndInit();
            //display jpg and change status
            origAudio.Source = origAudioBitmap;
            statusLatWav.Text = "Last wave Image Saved";
        }


        /* ------------------------------------------------------------------------------------------------------------
         * ------------------------- FFT calculation, Paint method, and other tools -----------------------------------
         * ------------------------------------------------------------------------------------------------------------
         */
        private void remove0Audio(ref byte[] oriData)
        {
            bool notZero = false;
            byte[] withoutStartData = new byte[oriData.Length + 1];
            int counter = 0;
            if (removeTypeCode == 1 || removeTypeCode == 3)
            {
                //Remove the start and the end of the audio
                for (int p = 0; p < oriData.Length - 1; p++)
                {

                    if (oriData[p] != 0x80 && notZero == false)
                    {
                        notZero = true;
                        int a = oriData.Length - p + 1;
                        withoutStartData = new byte[a];
                    }
                    if (notZero)
                    {
                        withoutStartData[counter] = oriData[p];
                        counter++;
                    }
                }

                //Remove the end of the audio
                notZero = false;
                int newLength = 0;
                for (int p = 35; p < withoutStartData.Length; p++)
                {
                    if (notZero == false)
                    {
                        newLength = withoutStartData.Length - p;
                    }
                    if (withoutStartData[withoutStartData.Length - p - 1] != 0x80)
                    {
                        notZero = true;
                    }
                }
                byte[] withoutStartandEndData = new byte[newLength];
                for (int p = 0; p < newLength; p++)
                    withoutStartandEndData[p] = withoutStartData[p];

                if (removeTypeCode == 3)
                {
                    bool contZeros = true;
                    ArrayList temp = new ArrayList();
                    for (int p = 0; p < withoutStartandEndData.Length - 30; p++)
                    {
                        for (int a = 0; a < 30; a++)
                        {
                            if (withoutStartandEndData[a + p] != 0x80)
                            {
                                contZeros = false;
                            }
                        }
                        if (contZeros)
                        {
                            p += 30;
                        }
                        else
                        {
                            temp.Add(withoutStartandEndData[p]);
                        }
                        contZeros = true;
                    }
                    byte[] newData = new byte[temp.Count];
                    for (int i = 0; i < temp.Count; i++)
                        newData[i] = (byte)temp[i];
                    oriData = newData;
                    return;
                }
                else
                {
                    oriData = withoutStartandEndData;
                    return;
                }

            }
            else if (removeTypeCode == 2)
            {
                counter = 0;
                for (int p = 0; p < oriData.Length; p++)
                {
                    if (oriData[p] == 0x80)
                    {
                        counter++;
                    }
                }
                counter = oriData.Length - counter;
                byte[] resultData = new byte[counter];
                counter = 0;
                for (int p = 0; p < oriData.Length; p++)
                {
                    if (oriData[p] != 0x80)
                    {
                        resultData[counter] = oriData[p];
                        counter++;
                    }
                }
                oriData = resultData;
                return;
            }
            else
                return;
        }
        private void Dit2_FFT(ref double[] data_r, ref double[] data_i, ref double[] result_r, ref double[] result_i)
        {
            if (data_r.Length == 0 || data_i.Length == 0 || data_r.Length != data_i.Length)
                return;
            int len = data_r.Length;
            double[] X_r = new double[len];
            double[] X_i = new double[len];
            for (int i = 0; i < len; i++)//将源数据复制副本，避免影响源数据的安全性  
            {
                X_r[i] = data_r[i];
                X_i[i] = data_i[i];
            }
            DataSort(ref X_r, ref X_i);//位置重排  
            double WN_r, WN_i;//旋转因子  
            int M = (int)(Math.Log(len) / Math.Log(2));//蝶形图级数  
            for (int l = 0; l < M; l++)
            {
                int space = (int)Math.Pow(2, l);
                int num = space;//旋转因子个数  
                double temp1_r, temp1_i, temp2_r, temp2_i;
                for (int i = 0; i < num; i++)
                {
                    int p = (int)Math.Pow(2, M - 1 - l);//同一旋转因子有p个蝶  
                    WN_r = Math.Cos(2 * Math.PI / len * p * i);
                    WN_i = -Math.Sin(2 * Math.PI / len * p * i);
                    for (int j = 0, n = i; j < p; j++, n += (int)Math.Pow(2, l + 1))
                    {
                        temp1_r = X_r[n];
                        temp1_i = X_i[n];
                        temp2_r = X_r[n + space];
                        temp2_i = X_i[n + space];
                        //为蝶形的两个输入数据作副本，对副本进行计算，避免数据被修改后参加下一次计算  
                        X_r[n] = temp1_r + temp2_r * WN_r - temp2_i * WN_i;
                        X_i[n] = temp1_i + temp2_i * WN_r + temp2_r * WN_i;
                        X_r[n + space] = temp1_r - temp2_r * WN_r + temp2_i * WN_i;
                        X_i[n + space] = temp1_i - temp2_i * WN_r - temp2_r * WN_i;
                    }
                }
            }
            result_r = X_r;
            result_i = X_i;
        }
        private void DataSort(ref double[] data_r, ref double[] data_i)
        {
            if (data_r.Length == 0 || data_i.Length == 0 || data_r.Length != data_i.Length)
                return;
            int len = data_r.Length;
            int[] count = new int[len];
            int M = (int)(Math.Log(len) / Math.Log(2));
            double[] temp_r = new double[len];
            double[] temp_i = new double[len];

            for (int i = 0; i < len; i++)
            {
                temp_r[i] = data_r[i];
                temp_i[i] = data_i[i];
            }
            for (int l = 0; l < M; l++)
            {
                int space = (int)Math.Pow(2, l);
                int add = (int)Math.Pow(2, M - l - 1);
                for (int i = 0; i < len; i++)
                {
                    if ((i / space) % 2 != 0)
                        count[i] += add;
                }
            }
            for (int i = 0; i < len; i++)
            {
                data_r[i] = temp_r[count[i]];
                data_i[i] = temp_i[count[i]];
            }
        }
        public System.Drawing.Image GetWaveFormImage(bool middleLine, int nWidth, int nHeight,
            System.Drawing.Color bkClr, System.Drawing.Color penClr, byte[] m_Data)
        {
            try
            {
                Bitmap bmp = null;
                bmp = new Bitmap(nWidth, nHeight);

                int BORDER_WIDTH = 0;
                int width = bmp.Width - (2 * BORDER_WIDTH);
                int height = bmp.Height - (2 * BORDER_WIDTH);

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(bkClr);
                    System.Drawing.Pen pen = new System.Drawing.Pen(penClr);

                    if (middleLine)
                    {
                        //画中间线
                        for (int iPixel = 0; iPixel < width; iPixel++)
                        {
                            float min = (float)((127 - 128) / 128.0);
                            float max = (float)((128 - 128) / 128.0);
                            int yMax = BORDER_WIDTH + height - (int)((max + 1) * .5 * height);
                            int yMin = BORDER_WIDTH + height - (int)((min + 1) * .5 * height);

                            g.DrawLine(pen, iPixel + BORDER_WIDTH, yMax,
                                    iPixel + BORDER_WIDTH, yMin);
                        }
                    }

                    //画有声的图
                    int size = m_Data.Length;
                    for (int iPixel = 0; iPixel < width; iPixel++)
                    {
                        // determine start and end points within WAV
                        int start = (int)((float)iPixel * ((float)size / (float)width));
                        int end = (int)((float)(iPixel + 1) * ((float)size / (float)width));
                        byte imin = byte.MaxValue;
                        byte imax = byte.MinValue;
                        for (int i = start; i < end; i++)
                        {
                            byte val = m_Data[i];
                            imin = val < imin ? val : imin;
                            imax = val > imax ? val : imax;
                        }
                        float min = (float)((imin - 128) / 128.0);
                        float max = (float)((imax - 128) / 128.0);
                        int yMax = BORDER_WIDTH + height - (int)((max + 1) * .5 * height);
                        int yMin = BORDER_WIDTH + height - (int)((min + 1) * .5 * height);
                        g.DrawLine(pen, iPixel + BORDER_WIDTH, yMax, iPixel + BORDER_WIDTH, yMin);
                    }
                }
                return bmp;
            }
            catch (Exception ex)
            {
                ex.ToString();
                return null;
            }
        }
        private void button_Click_1(object sender, RoutedEventArgs e)
        {
            if (fileOrFolder == "Folder")
            {
                System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog();
                System.Windows.Interop.HwndSource source = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
                System.Windows.Forms.IWin32Window win = new OldWindow(source.Handle);
                System.Windows.Forms.DialogResult result = dlg.ShowDialog(win);
                folderName.Text = dlg.SelectedPath;
            }
            else
            {
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
                openFileDialog.DefaultExt = "wav";
                openFileDialog.AddExtension = true;
                openFileDialog.InitialDirectory = @"C\Users\";
                openFileDialog.Title = "浏览文件";
                openFileDialog.ValidateNames = true;
                if (openFileDialog.ShowDialog() == true)
                {
                    folderName.Text = openFileDialog.FileName;
                }
            }
        }
        /* 
             * Below method is for the folder brower dialog
             * Somehow it just works :)
             * DO NOT ASK IF YOU HAVE TROUBLE WITH IT. :)
             */
        public class OldWindow : System.Windows.Forms.IWin32Window
        {
            IntPtr _handle;
            public OldWindow(IntPtr handle)
            {
                _handle = handle;
            }
            #region IWin32Window Members
            IntPtr System.Windows.Forms.IWin32Window.Handle
            {
                get { return _handle; }
            }
            #endregion
        }



        /* ------------------------------------------------------------------------------------------------------------
         * -------------------------------- event listeners and code initialization -----------------------------------
         * ------------------------------------------------------------------------------------------------------------
         *               passing the event listeners to actual values
         *               Ignore the below command while you check the code
         */
        private void radioButton_Checked(object sender, RoutedEventArgs e)
        {
            noiseDetection = (bool)radioButton.IsChecked;
        }
        private void radioButton_Copy_Checked(object sender, RoutedEventArgs e)
        {
            pitchDetection = (bool)radioButton_Copy.IsChecked;
        }
        private void graph_Checked(object sender, RoutedEventArgs e)
        {
            graphOutput = (bool)graph.IsChecked;
        }
        private void table_Checked(object sender, RoutedEventArgs e)
        {
            tableOutput = (bool)table.IsChecked;
        }
        private void sound_Checked(object sender, RoutedEventArgs e)
        {
            soundOutput = (bool)sound.IsChecked;
        }
        private void folderName_TextChanged(object sender, TextChangedEventArgs e)
        {
            folderNameTextboxChange = true;
        }
        private void fileSubfix_TextChanged(object sender, TextChangedEventArgs e)
        {
            subfixTextboxChange = true;
        }
        private void removeType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string removeTypeString = ((ComboBoxItem)removeType.SelectedItem).Content.ToString();
            switch (removeTypeString)
            {
                case "0. 掩耳盗铃":
                    removeTypeCode = 0;
                    break;
                case "1. 掐头去尾":
                    removeTypeCode = 1;
                    break;
                case "2. 群龙无首":
                    removeTypeCode = 2;
                    break;
                case "3. 有眼无珠":
                    removeTypeCode = 3;
                    break;
            }
        }
        private void comboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            fileOrFolder = ((ComboBoxItem)comboBox.SelectedItem).Content.ToString();
        }
        private void checkBox1_Checked(object sender, RoutedEventArgs e)
        {
            skipSevenF = (bool)sevenFs_Copy.IsChecked;
        }
        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            currTime = (float)(oritime * (float)slider.Value / 10);
            string str = " " + currTime.ToString() + " s";
            currentTime.Text = str;
            //playSegmentButton.IsEnabled = true;
        }
        private void secondSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            secondTime = (float)(oritime * (float)secondSlider.Value / 10);
            string str = " " + secondTime.ToString() + " s";
            secondTimeTextBox.Text = str;
        }
        private void refreshBtn_Click(object sender, RoutedEventArgs e)
        {
            refreshCounter++;
            FFTcountTextBlock.Text = refreshCounter.ToString();

            if (oritime < 0.125)
            {
                System.Windows.Forms.MessageBox.Show("Not enough time to do FFT.\nCheck the point again! \nError@line1025");
                return;
            }
            if (fileOrFolder == "Folder" && !(bool)checkBox.IsChecked)
            {
                System.Windows.Forms.MessageBox.Show("Not selected to do single file FFT! \nError@line1030");
                return;
            }
            if (currTime < 0.065)
            {
                currTime += (float)0.065;
            }

            refreshIOZYFFT();

            if (secondTime != 0)
                refreshSegmentFFT();

            saveCSVfile();
        }
        private void NextFileBtn_Click(object sender, RoutedEventArgs e)
        {
            wavFileCount++;
            currTime = 0;
            slider.Value = 0;
            currentTime.Text = "0";
            secondSlider.Value = 0;
            secondTimeTextBox.Text = "0";
            newHighestFrequcncy = 0;
            newSecondHighestFrequency = 0;
            sectionFFTbox.Source = null;
            //playSegmentButton.IsEnabled = false;
            player = new MediaPlayer();
            if (fileOrFolder == "File" || wavFileCount >= fileName.Count)
            {
                System.Windows.Forms.MessageBox.Show("No more file to read! \nError@line1057");
                return;
            }
            getAudioInfo((string)fileName[wavFileCount]);
        }
        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {
            singleFileOperation = (bool)checkBox.IsChecked;
        }
        private void playButton_Click(object sender, RoutedEventArgs e)
        {
            player.Play();
        }
        void InitEncode()
        {
            wav_encode = new byte[256];

            wav_encode[0x00] = 0x02;
            wav_encode[0x01] = 0x06;
            wav_encode[0x02] = 0x0B;
            wav_encode[0x03] = 0x0F;

            wav_encode[0x04] = 0x13;
            wav_encode[0x05] = 0x17;
            wav_encode[0x06] = 0x1B;
            wav_encode[0x07] = 0x1F;

            wav_encode[0x08] = 0x23;
            wav_encode[0x09] = 0x27;
            wav_encode[0x0A] = 0x2B;
            wav_encode[0x0B] = 0x2F;

            wav_encode[0x0C] = 0x33;
            wav_encode[0x0D] = 0x37;
            wav_encode[0x0E] = 0x3B;
            wav_encode[0x0F] = 0x3F;

            wav_encode[0x10] = 0x42;
            wav_encode[0x11] = 0x44;
            wav_encode[0x12] = 0x46;
            wav_encode[0x13] = 0x48;

            wav_encode[0x14] = 0x4A;
            wav_encode[0x15] = 0x4C;
            wav_encode[0x16] = 0x4E;
            wav_encode[0x17] = 0x50;

            wav_encode[0x18] = 0x52;
            wav_encode[0x19] = 0x54;
            wav_encode[0x1A] = 0x56;
            wav_encode[0x1B] = 0x58;

            wav_encode[0x1C] = 0x5A;
            wav_encode[0x1D] = 0x5C;
            wav_encode[0x1E] = 0x5E;
            wav_encode[0x1F] = 0x60;

            wav_encode[0x20] = 0x61;
            wav_encode[0x21] = 0x62;
            wav_encode[0x22] = 0x63;
            wav_encode[0x23] = 0x64;

            wav_encode[0x24] = 0x65;
            wav_encode[0x25] = 0x66;
            wav_encode[0x26] = 0x67;
            wav_encode[0x27] = 0x68;

            wav_encode[0x28] = 0x69;
            wav_encode[0x29] = 0x6A;
            wav_encode[0x2A] = 0x6B;
            wav_encode[0x2B] = 0x6C;

            wav_encode[0x2C] = 0x6D;
            wav_encode[0x2D] = 0x6E;
            wav_encode[0x2E] = 0x6F;
            wav_encode[0x2F] = 0x70;

            wav_encode[0x30] = 0x71;
            wav_encode[0x31] = 0x71;
            wav_encode[0x32] = 0x72;
            wav_encode[0x33] = 0x72;

            wav_encode[0x34] = 0x73;
            wav_encode[0x35] = 0x73;
            wav_encode[0x36] = 0x74;
            wav_encode[0x37] = 0x74;

            wav_encode[0x38] = 0x75;
            wav_encode[0x39] = 0x75;
            wav_encode[0x3A] = 0x76;
            wav_encode[0x3B] = 0x76;

            wav_encode[0x3C] = 0x77;
            wav_encode[0x3D] = 0x77;
            wav_encode[0x3E] = 0x78;
            wav_encode[0x3F] = 0x78;

            wav_encode[0x40] = 0x79;
            wav_encode[0x41] = 0x79;
            wav_encode[0x42] = 0x79;
            wav_encode[0x43] = 0x7A;

            wav_encode[0x44] = 0x7A;
            wav_encode[0x45] = 0x7A;
            wav_encode[0x46] = 0x7A;
            wav_encode[0x47] = 0x7B;

            wav_encode[0x48] = 0x7B;
            wav_encode[0x49] = 0x7B;
            wav_encode[0x4A] = 0x7B;
            wav_encode[0x4B] = 0x7C;

            wav_encode[0x4C] = 0x7C;
            wav_encode[0x4D] = 0x7C;
            wav_encode[0x4E] = 0x7C;
            wav_encode[0x4F] = 0x7D;

            wav_encode[0x50] = 0x7D;
            wav_encode[0x51] = 0x7D;
            wav_encode[0x52] = 0x7D;
            wav_encode[0x53] = 0x7D;

            wav_encode[0x54] = 0x7D;
            wav_encode[0x55] = 0x7E;
            wav_encode[0x56] = 0x7E;
            wav_encode[0x57] = 0x7E;

            wav_encode[0x58] = 0x7E;
            wav_encode[0x59] = 0x7E;
            wav_encode[0x5A] = 0x7E;
            wav_encode[0x5B] = 0x7E;

            wav_encode[0x5C] = 0x7E;
            wav_encode[0x5D] = 0x7F;
            wav_encode[0x5E] = 0x80;
            wav_encode[0x5F] = 0x80;

            wav_encode[0x60] = 0x80;
            wav_encode[0x61] = 0x80;
            wav_encode[0x62] = 0x80;
            wav_encode[0x63] = 0x80;

            wav_encode[0x64] = 0x80;
            wav_encode[0x65] = 0x80;
            wav_encode[0x66] = 0x80;
            wav_encode[0x67] = 0x80;

            wav_encode[0x68] = 0x80;
            wav_encode[0x69] = 0x80;
            wav_encode[0x6A] = 0x80;
            wav_encode[0x6B] = 0x80;

            wav_encode[0x6C] = 0x80;
            wav_encode[0x6D] = 0x80;
            wav_encode[0x6E] = 0x80;
            wav_encode[0x6F] = 0x80;

            wav_encode[0x70] = 0x80;
            wav_encode[0x71] = 0x80;
            wav_encode[0x72] = 0x80;
            wav_encode[0x73] = 0x80;

            wav_encode[0x74] = 0x80;
            wav_encode[0x75] = 0x80;
            wav_encode[0x76] = 0x80;
            wav_encode[0x77] = 0x80;

            wav_encode[0x78] = 0x80;
            wav_encode[0x79] = 0x80;
            wav_encode[0x7A] = 0x80;
            wav_encode[0x7B] = 0x80;

            wav_encode[0x7C] = 0x80;
            wav_encode[0x7D] = 0x80;
            wav_encode[0x7E] = 0x80;
            wav_encode[0x7F] = 0x80;

            wav_encode[0x80] = 0xFE;
            wav_encode[0x81] = 0xFA;
            wav_encode[0x82] = 0xF6;
            wav_encode[0x83] = 0xF2;

            wav_encode[0x84] = 0xEE;
            wav_encode[0x85] = 0xEA;
            wav_encode[0x86] = 0xE6;
            wav_encode[0x87] = 0xE2;

            wav_encode[0x88] = 0xDE;
            wav_encode[0x89] = 0xDA;
            wav_encode[0x8A] = 0xD6;
            wav_encode[0x8B] = 0xD2;

            wav_encode[0x8C] = 0xCE;
            wav_encode[0x8D] = 0xCA;
            wav_encode[0x8E] = 0xC6;
            wav_encode[0x8F] = 0xC2;

            wav_encode[0x90] = 0xBF;
            wav_encode[0x91] = 0xBD;
            wav_encode[0x92] = 0xBB;
            wav_encode[0x93] = 0xB9;

            wav_encode[0x94] = 0xB7;
            wav_encode[0x95] = 0xB5;
            wav_encode[0x96] = 0xB3;
            wav_encode[0x97] = 0xB1;

            wav_encode[0x98] = 0xAF;
            wav_encode[0x99] = 0xAD;
            wav_encode[0x9A] = 0xAB;
            wav_encode[0x9B] = 0xA9;

            wav_encode[0x9C] = 0xA7;
            wav_encode[0x9D] = 0xA5;
            wav_encode[0x9E] = 0xA3;
            wav_encode[0x9F] = 0xA1;

            wav_encode[0xA0] = 0x9F;
            wav_encode[0xA1] = 0x9E;
            wav_encode[0xA2] = 0x9D;
            wav_encode[0xA3] = 0x9C;

            wav_encode[0xA4] = 0x9B;
            wav_encode[0xA5] = 0x9A;
            wav_encode[0xA6] = 0x99;
            wav_encode[0xA7] = 0x98;

            wav_encode[0xA8] = 0x97;
            wav_encode[0xA9] = 0x96;
            wav_encode[0xAA] = 0x95;
            wav_encode[0xAB] = 0x94;

            wav_encode[0xAC] = 0x93;
            wav_encode[0xAD] = 0x92;
            wav_encode[0xAE] = 0x91;
            wav_encode[0xAF] = 0x90;

            wav_encode[0xB0] = 0x8F;
            wav_encode[0xB1] = 0x8F;
            wav_encode[0xB2] = 0x8E;
            wav_encode[0xB3] = 0x8E;

            wav_encode[0xB4] = 0x8D;
            wav_encode[0xB5] = 0x8D;
            wav_encode[0xB6] = 0x8C;
            wav_encode[0xB7] = 0x8C;

            wav_encode[0xB8] = 0x8B;
            wav_encode[0xB9] = 0x8B;
            wav_encode[0xBA] = 0x8A;
            wav_encode[0xBB] = 0x8A;

            wav_encode[0xBC] = 0x89;
            wav_encode[0xBD] = 0x89;
            wav_encode[0xBE] = 0x88;
            wav_encode[0xBF] = 0x88;

            wav_encode[0xC0] = 0x87;
            wav_encode[0xC1] = 0x87;
            wav_encode[0xC2] = 0x87;
            wav_encode[0xC3] = 0x86;

            wav_encode[0xC4] = 0x86;
            wav_encode[0xC5] = 0x86;
            wav_encode[0xC6] = 0x85;
            wav_encode[0xC7] = 0x85;

            wav_encode[0xC8] = 0x85;
            wav_encode[0xC9] = 0x85;
            wav_encode[0xCA] = 0x84;
            wav_encode[0xCB] = 0x84;

            wav_encode[0xCC] = 0x84;
            wav_encode[0xCD] = 0x84;
            wav_encode[0xCE] = 0x83;
            wav_encode[0xCF] = 0x83;

            wav_encode[0xD0] = 0x83;
            wav_encode[0xD1] = 0x83;
            wav_encode[0xD2] = 0x83;
            wav_encode[0xD3] = 0x83;

            wav_encode[0xD4] = 0x82;
            wav_encode[0xD5] = 0x82;
            wav_encode[0xD6] = 0x82;
            wav_encode[0xD7] = 0x82;

            wav_encode[0xD8] = 0x82;
            wav_encode[0xD9] = 0x82;
            wav_encode[0xDA] = 0x82;
            wav_encode[0xDB] = 0x82;

            wav_encode[0xDC] = 0x81;
            wav_encode[0xDD] = 0x81;
            wav_encode[0xDE] = 0x81;
            wav_encode[0xDF] = 0x81;

            wav_encode[0xE0] = 0x81;
            wav_encode[0xE1] = 0x81;
            wav_encode[0xE2] = 0x81;
            wav_encode[0xE3] = 0x81;

            wav_encode[0xE4] = 0x81;
            wav_encode[0xE5] = 0x81;
            wav_encode[0xE6] = 0x81;
            wav_encode[0xE7] = 0x81;

            wav_encode[0xE8] = 0x80;
            wav_encode[0xE9] = 0x80;
            wav_encode[0xEA] = 0x80;
            wav_encode[0xEB] = 0x80;

            wav_encode[0xEC] = 0x80;
            wav_encode[0xED] = 0x80;
            wav_encode[0xEE] = 0x80;
            wav_encode[0xEF] = 0x80;

            wav_encode[0xF0] = 0x80;
            wav_encode[0xF1] = 0x80;
            wav_encode[0xF2] = 0x80;
            wav_encode[0xF3] = 0x80;

            wav_encode[0xF4] = 0x80;
            wav_encode[0xF5] = 0x80;
            wav_encode[0xF6] = 0x80;
            wav_encode[0xF7] = 0x80;

            wav_encode[0xF8] = 0x80;
            wav_encode[0xF9] = 0x80;
            wav_encode[0xFA] = 0x80;
            wav_encode[0xFB] = 0x80;

            wav_encode[0xFC] = 0x80;
            wav_encode[0xFD] = 0x80;
            wav_encode[0xFE] = 0x80;
            wav_encode[0xFF] = 0x80;
        }
    }
}
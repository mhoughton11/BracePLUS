﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Syncfusion.SfChart.XForms;
using System.Threading.Tasks;
using Xamarin.Forms;
using BracePLUS.Extensions;
using System.Diagnostics;
using System.IO;
using Xamarin.Essentials;

namespace BracePLUS.Models
{
    public class DataObject : BindableObject
    {
        private MessageHandler handler;

        // File Properties
        public bool IsDownloaded { get; set; }
        public DateTime Date { get; set; }
        public string Filename { get; set; }
        public string ShortFilename { get; set; }
        public string Location { get; set; }
        public long Size { get; set; }
        public double Duration { get; set; }

        // View Properties
        public string Text
        {
            get { return BitConverter.ToString(Data); }
            set { }
        }
        public string FormattedSize
        {
            get { return handler.FormattedFileSize(Size); }
            set { }
        }
        public string DataString
        { 
            get { return getPreviewDataString(); }
            set { }
        }
        public string Name 
        { 
            get { return String.Format($"{Date.ToShortDateString()}, {Date.ToShortTimeString()}"); }
            set { }
        }

        // Data Properties
        public double AveragePressure { get; set; }
        public double MaxPressure { get; set; }
        public byte[] Data { get; set; }
        public ObservableCollection<ChartDataModel> NormalData { get; set; }
        public ObservableCollection<ChartDataModel> PreviewNormalData { get; set; }
        public string Detail
        {
            get { return string.Format("{0}, {1:0.00}s", FormattedSize, Duration); }
            set { }
        }
        public bool Favourite { get; set; }
        public string FavIcon 
        { 
            get { 
                    if (Favourite)
                    {
                        return "StarFilled.png";
                    }
                    else
                    {
                        return "StarUnfilled.png";
                    }
            }
            set { }
        }

        // Commands
        public Command FavouriteClicked { get; set; }
        public Command ShareClicked { get; set; }

        public DataObject()
        {
            handler = new MessageHandler();
            NormalData = new ObservableCollection<ChartDataModel>();
            PreviewNormalData = new ObservableCollection<ChartDataModel>();

            ShareClicked = new Command(async () => await ExecuteShareCommand());
            FavouriteClicked = new Command(() => { Favourite = !Favourite; });
        }
        
        public bool DownloadData(string path)
        {
            if (!IsDownloaded)
            {
                try
                {
                    IsDownloaded = true;
                    Data = File.ReadAllBytes(path);

                    int packets = (Data.Length - 6) / 128;

                    // Prepare chart data
                    var normals = handler.ExtractNormals(Data, packets, 11);

                    Duration = getDuration();
                    AveragePressure = getAverage(normals);
                    MaxPressure = getMaximum(normals);

                    int x = 100;
                    if (normals.Count < x) x = normals.Count;

                    for (int i = 0; i < x; i++)
                    {
                        PreviewNormalData.Add(new ChartDataModel(i.ToString(), normals[i]));
                    }
                    for (int i = 0; i < packets; i++)
                    {
                        NormalData.Add(new ChartDataModel(i.ToString(), normals[i]));
                    }
                }
                catch (Exception ex)
                {
                    IsDownloaded = false;
                    Debug.WriteLine("Data download failed with exception: " + ex.Message);
                }
            }

            return IsDownloaded;
        }

        public double getDuration()
        {
            if (IsDownloaded)
            {
                // Extract first and last time packets from file.
                // File format: [ 0x0A | 0x0B | 0x0C | T0 | T1 | T2 | T3 | X1MSB.....| Zn | 0x0A | 0x0B | 0x0C ]
                byte t3 = Data[6];
                byte t2 = Data[5];
                byte t1 = Data[4];
                byte t0 = Data[3];
                var t_start = t0 + (t1 << 8) + (t2 << 16) + (t3 << 24);

                int length = Data.Length;

                // Packet length is 128, then accomodate for file footer.
                // Last time packet is bytes 0:3 of last packet
                t3 = Data[length - 128];
                t2 = Data[length - 129];
                t1 = Data[length - 130];
                t0 = Data[length - 131];

                var t_finish = t0 + (t1 << 8) + (t2 << 16) + (t3 << 24);

                return (t_finish - t_start) / 1000.0;
            }
            else
            {
                Debug.WriteLine("Data not downloaded, unable to extract duration.");
            }

            return 0;
        }

        public double getAverage(List<double> values)
        {
            int length = values.Count;
            double sum = 0.0;
            foreach (double val in values) sum += val;

            return sum / length;
        }

        public double getMaximum(List<double> values)
        {
            int length = values.Count;
            double max = 0.0;

            foreach (double val in values)
            {
                if (val > max) max = val;
            }

            return max;
        }

        public async Task ExecuteShareCommand()
        {
            var file = Path.Combine(App.FolderPath, Filename);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = ShortFilename,
                File = new ShareFile(file)
            });
        }

        private string getPreviewDataString()
        {
            if (Data.Length < 100)
            {
                return BitConverter.ToString(Data);
            }
            else
            {
                // Create 100 char string of data and append "..." 
                return BitConverter.ToString(Data).Substring(0, 100).Insert(100, "...");
            }
        }
    }
}
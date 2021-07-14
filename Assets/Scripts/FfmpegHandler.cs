using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Text;
using System.Linq;
using System.Globalization;

// Script that handles all video encoding functionality.
public class FfmpegHandler : MonoBehaviour
{
    // Processes for various encoding functions.
    Process encoder;
    Process encodedFrameExtractor;
    Process rawFrameExtractor;
    Process frameCounter;
    Process videoExporter;

    // Path to location where to store the encoded video chunk.
    string videoEncodingPath = (Application.streamingAssetsPath + "/EncodedChunk/").Replace("/", "\\");

    // Path to location where to store the raw video frames.
    string rawFramesPath = (Application.streamingAssetsPath + "/RawFrames/").Replace("/", "\\");

    // Path to location where to store the encoded video frames.
    string encodedFramesPath = (Application.streamingAssetsPath + "/EncodedFrames/").Replace("/", "\\");

    // DirectoryInfo variables for the previous paths.
    // Used to delete files in the specified folders.
    DirectoryInfo rawFramesSizeInfo;
    DirectoryInfo encodedFramesSizeInfo;
    DirectoryInfo encodedChunkInfo;

    // Strings containing arguments to start the processes.
    // Updated via functions in code.
    string rawFramesExtractorArguments;
    string encoderArguments;
    string encodedFramesExtractorArguments;
    string frameCounterArguments;
    string videoExporterArguments;

    // Variables for storing information for the input .yuv file.
    string fileLocation;
    int pixelFormatIndex;
    int codecIndex;
    int containerIndex;
    int width;
    int height;
    long bitrate;
    int frameRate;
    string inputFilePixelFormat;

    // Boolean variables to monitor the stage of the encoding process.
    bool canEncodeNextBlock;
    bool endEncoding;
    bool canExtractEncodedFrames;
    bool canUpdateEncodedFrameData;

    // Variables to track the progress of the encoding process.
    int currentEncodedFrameIndex = 0;
    int nextFrameIndex = -1;
    int targetEncodingFrameblockSize = 100;
    int encoderFrameBlockSize;
    string temporaryEncoderOutput;
    bool[] isFrameEncodedDictionary;

    // Constant used to verify that the size of the raw frames folder does not exceed 3GB.
    private readonly long maxFrameFolderSize = 3000000000;

    // Contains the total number of frames present in the input .yuv file.
    int totalFrames;

    // Variables used to calculate the speed of the encoding process.
    DateTime encodingStart;
    DateTime encodingEnd;

    // List containing info on all encoded frame blocks.
    List<FrameBlock> EncodedFrameBlocksIndexList = new List<FrameBlock>();

    // String array containing supported codec names.
    private readonly string[] supportedCodecs = { "libx264", "libx265" };

    // String array containing supported YUV sampling format's naames.
    private readonly string[] supportedPixelFormats = { "yuv420p", "yuv422p", "yuv444p", "yuv410p", "yuv411p" };

    // String array containing supported video container's names.
    private readonly string[] supportedContainerFormats = { "mp4", "mkv", "avi" };

    // Start is called before the first frame update.
    void Start()
    {
        // Initialize all processes.
        encoder = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = (Application.streamingAssetsPath + "/Ffmpeg/ffmpeg.exe").Replace("/", "\\"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        encodedFrameExtractor = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = (Application.streamingAssetsPath + "/Ffmpeg/ffmpeg.exe").Replace("/", "\\"),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        rawFrameExtractor = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = (Application.streamingAssetsPath + "/Ffmpeg/ffmpeg.exe").Replace("/", "\\"),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        frameCounter = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = (Application.streamingAssetsPath + "/Ffmpeg/ffprobe.exe").Replace("/", "\\"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        videoExporter = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = (Application.streamingAssetsPath + "/Ffmpeg/ffmpeg.exe").Replace("/", "\\"),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        // Create all needed directories.
        Directory.CreateDirectory(rawFramesPath);
        Directory.CreateDirectory(encodedFramesPath);
        Directory.CreateDirectory(videoEncodingPath);

        // Initialize DirectoryInfo variables.
        rawFramesSizeInfo = new DirectoryInfo(rawFramesPath);
        encodedFramesSizeInfo = new DirectoryInfo(encodedFramesPath);
        encodedChunkInfo = new DirectoryInfo(videoEncodingPath);

        // Set the encoded block size to its default value (100).
        encoderFrameBlockSize = targetEncodingFrameblockSize;

        // Delete any possible previous encoding data.
        DeleteAllEncodingData();

        // Make sure encoding process does not start yet.
        this.endEncoding = true;
    }

    // Called when a user has provided all needed info for import of a .yuv file.
    public void SetEncodingParameters(string desiredFileLocation, int desiredPixelFormatIndex, int resWidth, int resHeight, int frameRate, int recommendedBitrate)
    {
        // Delete previous encoding data.
        DeleteAllEncodingData();
        // Reset video playback.
        ResetDisplayFrame();

        // Set all info for the .yuv file.
        this.fileLocation = desiredFileLocation;
        this.pixelFormatIndex = desiredPixelFormatIndex;
        this.width = resWidth;
        this.height = resHeight;
        this.frameRate = frameRate;
        this.codecIndex = 0;
        this.containerIndex = 0;
        this.bitrate = recommendedBitrate;

        // Set the frame block size in such a way to encode 10 seconds of footage at a time.
        this.targetEncodingFrameblockSize = frameRate * 10;

        // Calculate the number of frames in the .yuv file.
        CalculateTotalFrames();
        // Forward total number of frames to other scripts.
        SendTotalFramesToVideoPlayer();
        SendTotalFramesToUserInterfaceHandler();
        // Initialize the boolean array for tracking which frames have been encoded.
        isFrameEncodedDictionary = new bool[totalFrames];

        // Set encoding to start from the first frame.
        this.currentEncodedFrameIndex = 0;
        // Reset the encoding process.
        ResetChunkEncodingProcess();
    }

    // Returns a List containing info on all encoded frame blocks.
    public List<FrameBlock> GetEncodedFrameBlocksIndexList()
    {
        return EncodedFrameBlocksIndexList;
    }

    // Resets the video playback to the first frame of the video.
    void ResetDisplayFrame()
    {
        gameObject.GetComponent<FramePlayback>().SeekToFrame(0);
    }

    // Forward total number of frames to the VideoPlayer script.
    void SendTotalFramesToVideoPlayer()
    {
        gameObject.GetComponent<FramePlayback>().SetTotalNumberOfFrames(this.totalFrames);
    }

    // Forward total number of frames to the InterfaceHandler script.
    void SendTotalFramesToUserInterfaceHandler()
    {
        gameObject.GetComponent<UserInterfaceHandler>().SetTotalNumberOfFrames(this.totalFrames);
    }

    // Uses an Ffprobe process to caalculate total number of frames in the imported .yuv file.
    void CalculateTotalFrames()
    {
        frameCounterArguments = "-v error ";
        frameCounterArguments += "-count_frames ";
        frameCounterArguments += "-select_streams v:0 ";
        frameCounterArguments += "-show_entries stream=nb_read_frames ";
        frameCounterArguments += "-of csv=p=0 ";
        frameCounterArguments += "-f rawvideo ";
        frameCounterArguments += "-pixel_format " + this.supportedPixelFormats[pixelFormatIndex] + " ";
        frameCounterArguments += "-video_size " + this.width + "x" + this.height + " ";
        frameCounterArguments += "\"" + this.fileLocation + "\"";

        print(frameCounterArguments);
        frameCounter.StartInfo.Arguments = frameCounterArguments;
        frameCounter.Start();
        string output = frameCounter.StandardOutput.ReadToEnd();
        int numberOfFrames = int.Parse(output);
        frameCounter.WaitForExit();

        totalFrames = numberOfFrames;
    }

    // Uses an Ffmpeg process to export the encoded video to the host PC's file system.
    public void ExportVideo(string exportPath)
    {
        string videoExporterArguments = "-f rawvideo ";
        videoExporterArguments += "-pix_fmt " + this.supportedPixelFormats[pixelFormatIndex] + " ";
        videoExporterArguments += "-s:v " + this.width + "x" + this.height + " ";
        videoExporterArguments += "-r " + this.frameRate + " ";
        videoExporterArguments += "-i \"" + this.fileLocation + "\" ";
        videoExporterArguments += "-c:v " + this.supportedCodecs[codecIndex] + " ";
        videoExporterArguments += "-b " + this.bitrate + " ";
        videoExporterArguments += "\"" + exportPath + "\"";

        print("EPORTING: " + videoExporterArguments);
        videoExporter.StartInfo.Arguments = videoExporterArguments;

        videoExporter.Start();
        videoExporter.WaitForExit();
    }

    // Returns total number of frames in the imported .yuv file.
    public int GetTotalFrames()
    {
        return totalFrames;
    }

    // Returns the currently active video container format.
    public string GetCurrentContainerFormat()
    {
        return this.supportedContainerFormats[containerIndex];
    }

    // Updates parameters that the user can affect such as Bitrate, Codec and Video Container.
    public void UpdateEncoderParameters(long desiredBitrate, int desiredCodecIndex, int desiredContainerIndex)
    {
        ResetChunkEncodingProcess();
        this.bitrate = desiredBitrate * 1000;
        this.codecIndex = desiredCodecIndex;
        this.containerIndex = desiredContainerIndex;
        UpdateCurrentEncodedFrameIndex(gameObject.GetComponent<FramePlayback>().GetCurrentDisplayFrame());
        DeleteAllEncodingData();

        print(this.bitrate);
    }

    // Updates the index of the last encoded frame.
    public void UpdateCurrentEncodedFrameIndex(int currentFrame)
    {
        this.nextFrameIndex = currentFrame;
        this.endEncoding = false;
    }

    // Resets the flags for the encoding process.
    void ResetChunkEncodingProcess()
    {
        endEncoding = false;
        canEncodeNextBlock = true;
        canExtractEncodedFrames = false;
        canUpdateEncodedFrameData = false;
    }

    // Deletes all the stored encoded frames and resets the bool array for tracking encoded frames.
    void DeleteAllEncodingData()
    {
        foreach (FileInfo frame in rawFramesSizeInfo.GetFiles())
        {
            frame.Delete();
        }
        foreach (FileInfo frame in encodedFramesSizeInfo.GetFiles())
        {
            frame.Delete();
        }
        foreach (FileInfo chunk in encodedChunkInfo.GetFiles())
        {
            chunk.Delete();
        }

        isFrameEncodedDictionary = new bool[totalFrames];
        EncodedFrameBlocksIndexList.Clear();
    }

    // Deletes the stored encoded frames and resets the bool array for tracking encoded frames, for a specific frame range.
    void DeleteFrameRange(int startIndex, int endIndex)
    {
        foreach (FileInfo frame in rawFramesSizeInfo.GetFiles())
        {
            string frameNumber = frame.Name.Split('.')[0];
            int frameIndex = int.Parse(frameNumber);
            if ((frameIndex >= startIndex) && (frameIndex <= endIndex))
            {
                frame.Delete();
            }
        }

        foreach (FileInfo frame in encodedFramesSizeInfo.GetFiles())
        {
            string frameNumber = frame.Name.Split('.')[0];
            int frameIndex = int.Parse(frameNumber);
            if ((frameIndex >= startIndex) && (frameIndex <= endIndex))
            {
                frame.Delete();
            }
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            isFrameEncodedDictionary[i] = false;
        }
    }

    // Deletes the video file that contains the 10 seconds of encoded footage.
    void DeleteEncodedChunk()
    {
        foreach (FileInfo chunk in encodedChunkInfo.GetFiles())
        {
            chunk.Delete();
        }
    }

    // Updates the arguments for the encoder process.
    void EncoderUpdate()
    {
        encoderArguments = "-f rawvideo ";
        encoderArguments += "-pix_fmt " + this.supportedPixelFormats[pixelFormatIndex] + " ";
        encoderArguments += "-s:v " + this.width + "x" + this.height + " ";
        encoderArguments += "-r " + this.frameRate + " ";
        encoderArguments += "-i \"" + this.fileLocation + "\" ";
        encoderArguments += "-codec:v " + this.supportedCodecs[codecIndex] + " ";
        encoderArguments += "-ssim 1 -psnr ";
        encoderArguments += "-b:v " + this.bitrate + " ";
        encoderArguments += "-vf select=between(n\\," + this.currentEncodedFrameIndex + "\\," + (this.currentEncodedFrameIndex + this.encoderFrameBlockSize - 1) + ") ";

        if(codecIndex == 1)
        {
            encoderArguments += "-x265-params ssim=1 ";
        }

        encoderArguments += "\"" + this.videoEncodingPath + "output." + this.supportedContainerFormats[containerIndex] + "\"";

        print(encoderArguments);
        encoder.StartInfo.Arguments = encoderArguments;
    }

    // Updates the arguments for the encoded frame extractor process.
    void EncodedFrameExtractorUpdate()
    {
        encodedFramesExtractorArguments = "-i \"" + this.videoEncodingPath + "output." + this.supportedContainerFormats[containerIndex] + "\" ";
        encodedFramesExtractorArguments += "-vsync 0 ";
        encodedFramesExtractorArguments += "-start_number " + this.currentEncodedFrameIndex + " ";
        encodedFramesExtractorArguments += "\"" + this.encodedFramesPath + "frame%d.png\"";

        print(encodedFramesExtractorArguments);
        encodedFrameExtractor.StartInfo.Arguments = encodedFramesExtractorArguments;
    }

    // Updates the arguments for the raw frame extractor process.
    void RawFrameExtractorUpdate()
    {
        rawFramesExtractorArguments = "-video_size " + this.width + "x" + this.height + " ";
        rawFramesExtractorArguments += "-pixel_format " + this.supportedPixelFormats[pixelFormatIndex] + " ";
        rawFramesExtractorArguments += "-vsync 0 ";
        rawFramesExtractorArguments += "-i \"" + this.fileLocation + "\" ";
        rawFramesExtractorArguments += "-start_number " + this.currentEncodedFrameIndex + " ";
        rawFramesExtractorArguments += "-vf select=between(n\\," + this.currentEncodedFrameIndex + "\\," + (this.currentEncodedFrameIndex + this.encoderFrameBlockSize - 1) + ") ";
        rawFramesExtractorArguments += "\"" + this.rawFramesPath + "frame%d.png\"";

        print(rawFramesExtractorArguments);
        rawFrameExtractor.StartInfo.Arguments = rawFramesExtractorArguments;
    }

    // Calculates the statring index and size of the next chunk of footage to be encoded.
    void CalculateNextEncodingIndexes()
    {
        int frameIndex;

        for (frameIndex = this.currentEncodedFrameIndex; frameIndex < this.currentEncodedFrameIndex + this.targetEncodingFrameblockSize && frameIndex < this.totalFrames; frameIndex++)
        {
            print("INDX=" + frameIndex);
            print(isFrameEncodedDictionary.Length);
            if(isFrameEncodedDictionary[frameIndex] == false)
            {
                break;
            }
        }
        if (frameIndex >= this.totalFrames)
        {
            this.endEncoding = true;
        }
        else if (frameIndex >= this.currentEncodedFrameIndex + this.targetEncodingFrameblockSize)
        {
            this.currentEncodedFrameIndex += this.targetEncodingFrameblockSize;
            CalculateNextEncodingIndexes();
        }
        else
        {
            this.currentEncodedFrameIndex = frameIndex;

            for (frameIndex = this.currentEncodedFrameIndex; frameIndex < this.currentEncodedFrameIndex + this.targetEncodingFrameblockSize && frameIndex < this.totalFrames; frameIndex++)
            {
                if (isFrameEncodedDictionary[frameIndex] == true)
                {
                    break;
                }
            }

            this.encoderFrameBlockSize = frameIndex - this.currentEncodedFrameIndex;
        }
    }

    // Updates the booleaan array for encoded frames and generates the corresponding frameblock.
    void UpdateEncodedFrameData()
    {
        int indexCounter;
        for (indexCounter = this.currentEncodedFrameIndex; indexCounter < this.currentEncodedFrameIndex + this.encoderFrameBlockSize && indexCounter < this.totalFrames; indexCounter++)
        {
            isFrameEncodedDictionary[indexCounter] = true;
        }

        FrameBlock frameBlock = new FrameBlock(this.currentEncodedFrameIndex, indexCounter - 1);
        ParseProcessOutput(frameBlock);
        CalculateBlockEncodingTime(frameBlock);
        EncodedFrameBlocksIndexList.Add(frameBlock);
    }

    // Calculates the time required to encode a chunk of the video, then stores the data in a frameblock.
    void CalculateBlockEncodingTime(FrameBlock currentFrameBlock)
    {
        currentFrameBlock.framesEncodedPerSecond = currentFrameBlock.frameBlockSize / ((encodingEnd - encodingStart).TotalSeconds);
    }

    // Detects and deletes the least relevant chunk of encoded frames.
    // The least relevant chunk is the chunk with the lowest frame indexes that is currently not being used for video playback.
    // Otherwise, the least relevant chunk is the one with the highest frame indexes.
    void DeleteLeastRelevantEncodedFrameBlock()
    {
        FrameBlock latestFrameBlock = new FrameBlock(-1, -1);
        FrameBlock earliestFrameBlock = new FrameBlock(this.totalFrames, this.totalFrames);
        int currentDisplayFrame = gameObject.GetComponent<FramePlayback>().GetCurrentDisplayFrame();

        foreach (FrameBlock encodedFrameBlock in EncodedFrameBlocksIndexList)
        {
            if (earliestFrameBlock.endFrameIndex > encodedFrameBlock.endFrameIndex)
            {
                earliestFrameBlock = encodedFrameBlock;
            }
            if (latestFrameBlock.endFrameIndex < encodedFrameBlock.endFrameIndex)
            {
                latestFrameBlock = encodedFrameBlock;
            }
        }
        if (currentDisplayFrame < earliestFrameBlock.endFrameIndex)
        {
            DeleteFrameRange(earliestFrameBlock.startFrameIndex, earliestFrameBlock.endFrameIndex);
            EncodedFrameBlocksIndexList.Remove(earliestFrameBlock);
        }
        else
        {
            DeleteFrameRange(latestFrameBlock.startFrameIndex, latestFrameBlock.endFrameIndex);
            EncodedFrameBlocksIndexList.Remove(latestFrameBlock);
        }
    }

    // Parses the encoder process standard output to gaather vaalues like PSNR, SSIM aand number if I-Frames.
    void ParseProcessOutput(FrameBlock currentFrameBlock)
    {
        char[] temporaryEncoderOutputCharArr = this.temporaryEncoderOutput.ToCharArray();
        bool numberHasNotStarted = true;

        if (codecIndex == 0)
        {
            int temporaryOutputStartIndex = this.temporaryEncoderOutput.IndexOf("frame I:");
            temporaryOutputStartIndex += 8;
            int temporaryOutputEndIndex = temporaryOutputStartIndex;

            while (temporaryOutputEndIndex < temporaryEncoderOutputCharArr.Length)
            {
                if (Char.IsNumber(temporaryEncoderOutputCharArr[temporaryOutputEndIndex]))
                {
                    temporaryOutputEndIndex++;
                }
                else
                {
                    break;
                }
            }

            currentFrameBlock.numberOfIFrames = int.Parse(this.temporaryEncoderOutput.Substring(temporaryOutputStartIndex, temporaryOutputEndIndex - temporaryOutputStartIndex));
            print("IFRAMES= " + currentFrameBlock.numberOfIFrames);

            temporaryOutputStartIndex = this.temporaryEncoderOutput.IndexOf("SSIM Mean Y:");
            temporaryOutputStartIndex += 12;
            temporaryOutputEndIndex = temporaryOutputStartIndex;

            while (temporaryOutputEndIndex < temporaryEncoderOutputCharArr.Length)
            {
                if (Char.IsNumber(temporaryEncoderOutputCharArr[temporaryOutputEndIndex]))
                {
                    temporaryOutputEndIndex++;
                }
                else if (temporaryEncoderOutputCharArr[temporaryOutputEndIndex] == '.')
                {
                    temporaryOutputEndIndex += 6;
                    break;
                }
                else
                {
                    break;
                }
            }

            currentFrameBlock.avgSsim = double.Parse(this.temporaryEncoderOutput.Substring(temporaryOutputStartIndex, temporaryOutputEndIndex - temporaryOutputStartIndex));

            temporaryOutputStartIndex = this.temporaryEncoderOutput.IndexOf("PSNR Mean Y:");
            temporaryOutputStartIndex = this.temporaryEncoderOutput.IndexOf("Global:", temporaryOutputStartIndex);
            temporaryOutputStartIndex += 7;
            temporaryOutputEndIndex = temporaryOutputStartIndex;

            while (temporaryOutputEndIndex < temporaryEncoderOutputCharArr.Length)
            {
                if (Char.IsNumber(temporaryEncoderOutputCharArr[temporaryOutputEndIndex]))
                {
                    temporaryOutputEndIndex++;
                }
                else if (temporaryEncoderOutputCharArr[temporaryOutputEndIndex] == '.')
                {
                    temporaryOutputEndIndex += 3;
                    break;
                }
                else
                {
                    break;
                }
            }

            currentFrameBlock.avgPsnr = double.Parse(this.temporaryEncoderOutput.Substring(temporaryOutputStartIndex, temporaryOutputEndIndex - temporaryOutputStartIndex));
        }
        else if (codecIndex == 1)
        {
            int temporaryOutputStartIndex = this.temporaryEncoderOutput.IndexOf("frame I:");
            temporaryOutputStartIndex += 8;
            int temporaryOutputEndIndex = temporaryOutputStartIndex;

            while (temporaryOutputEndIndex < temporaryEncoderOutputCharArr.Length)
            {
                if (Char.IsWhiteSpace(temporaryEncoderOutputCharArr[temporaryOutputEndIndex]) && numberHasNotStarted)
                {
                    temporaryOutputStartIndex++;
                    temporaryOutputEndIndex++;
                }
                else if (Char.IsNumber(temporaryEncoderOutputCharArr[temporaryOutputEndIndex]))
                {
                    numberHasNotStarted = false;
                    temporaryOutputEndIndex++;
                }
                else
                {
                    numberHasNotStarted = true;
                    break;
                }
            }

            currentFrameBlock.numberOfIFrames = int.Parse(this.temporaryEncoderOutput.Substring(temporaryOutputStartIndex, temporaryOutputEndIndex - temporaryOutputStartIndex));

            temporaryOutputStartIndex = this.temporaryEncoderOutput.IndexOf("SSIM Mean Y:");
            temporaryOutputStartIndex += 12;
            temporaryOutputEndIndex = temporaryOutputStartIndex;

            while (temporaryOutputEndIndex < temporaryEncoderOutputCharArr.Length)
            {
                if (Char.IsWhiteSpace(temporaryEncoderOutputCharArr[temporaryOutputEndIndex]) && numberHasNotStarted)
                {
                    temporaryOutputStartIndex++;
                    temporaryOutputEndIndex++;
                }
                else if (Char.IsNumber(temporaryEncoderOutputCharArr[temporaryOutputEndIndex]))
                {
                    numberHasNotStarted = false;
                    temporaryOutputEndIndex++;
                }
                else if (temporaryEncoderOutputCharArr[temporaryOutputEndIndex] == '.')
                {
                    numberHasNotStarted = true;
                    temporaryOutputEndIndex += 6;
                    break;
                }
                else
                {
                    numberHasNotStarted = true;
                    break;
                }
            }

            currentFrameBlock.avgSsim = double.Parse(this.temporaryEncoderOutput.Substring(temporaryOutputStartIndex, temporaryOutputEndIndex - temporaryOutputStartIndex));

            temporaryOutputStartIndex = this.temporaryEncoderOutput.IndexOf("Global PSNR:");
            temporaryOutputStartIndex += 12;
            temporaryOutputEndIndex = temporaryOutputStartIndex;

            while (temporaryOutputEndIndex < temporaryEncoderOutputCharArr.Length)
            {
                if (Char.IsWhiteSpace(temporaryEncoderOutputCharArr[temporaryOutputEndIndex]) && numberHasNotStarted)
                {
                    temporaryOutputStartIndex++;
                    temporaryOutputEndIndex++;
                }
                else if (Char.IsNumber(temporaryEncoderOutputCharArr[temporaryOutputEndIndex]))
                {
                    numberHasNotStarted = false;
                    temporaryOutputEndIndex++;
                }
                else if (temporaryEncoderOutputCharArr[temporaryOutputEndIndex] == '.')
                {
                    numberHasNotStarted = true;
                    temporaryOutputEndIndex += 3;
                    break;
                }
                else
                {
                    numberHasNotStarted = true;
                    break;
                }
            }

            currentFrameBlock.avgPsnr = double.Parse(this.temporaryEncoderOutput.Substring(temporaryOutputStartIndex, temporaryOutputEndIndex - temporaryOutputStartIndex));
        }
    }

    // Calculates if the folder containing the raw frames has exceeded the desired size.
    bool IsFrameFolderSizeTooLarge()
    {
        long FramesFolderSize = rawFramesSizeInfo.EnumerateFiles().Sum(File => File.Length);
        print("size raw = " + FramesFolderSize);
        if (FramesFolderSize > this.maxFrameFolderSize)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    // Update is called once per frame.
    void Update()
    {
        if (!endEncoding)
        {
            if (canEncodeNextBlock)
            {
                canEncodeNextBlock = false;

                if (this.nextFrameIndex >= 0)
                {
                    this.currentEncodedFrameIndex = this.nextFrameIndex;
                    this.nextFrameIndex = -1;
                }

                CalculateNextEncodingIndexes();

                if (!endEncoding)
                {
                    EncoderUpdate();
                    EncodedFrameExtractorUpdate();
                    RawFrameExtractorUpdate();

                    encodingStart = DateTime.Now;
                    encoder.Start();
                    rawFrameExtractor.Start();
                    this.temporaryEncoderOutput = encoder.StandardError.ReadToEnd();

                    canExtractEncodedFrames = true;
                }
            }
            else if (File.Exists(videoEncodingPath + "output." + supportedContainerFormats[containerIndex]) && canExtractEncodedFrames)
            {
                canExtractEncodedFrames = false;
                encodingEnd = encoder.ExitTime;
                encodedFrameExtractor.Start();
                canUpdateEncodedFrameData = true;
            }
            else if (canUpdateEncodedFrameData && rawFrameExtractor.HasExited && encodedFrameExtractor.HasExited)
            {
                canUpdateEncodedFrameData = false;
                DeleteEncodedChunk();
                UpdateEncodedFrameData();

                this.currentEncodedFrameIndex += this.encoderFrameBlockSize;
                this.encoderFrameBlockSize = this.targetEncodingFrameblockSize;

                if (this.currentEncodedFrameIndex == this.totalFrames)
                {
                    this.endEncoding = true;
                }

                while (IsFrameFolderSizeTooLarge())
                {
                    DeleteLeastRelevantEncodedFrameBlock();
                }
                canEncodeNextBlock = true;
            }
        }
    }

    // OnApplicationQuit is called when exiting the application.
    void OnApplicationQuit()
    {
        DeleteAllEncodingData();
    }
}

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Text;

public class FramePlayback : MonoBehaviour
{
    // RawImage GameObject reference for both video feeds.
    public RawImage uncompressedFeed;
    public RawImage compressedFeed;

    // Texture2D element to be applied on top of the RawImage GameObject.
    Texture2D uncompressedTexture;
    Texture2D compressedTexture;

    // Paths to the folders containing the frames for the video feeds.
    string rawFramesPath = (Application.streamingAssetsPath + "/RawFrames/").Replace("/", "\\");
    string encodedFramesPath = (Application.streamingAssetsPath + "/EncodedFrames/").Replace("/", "\\");

    // Variables to track video playback status.
    int totalNumberOfFrames;
    int currentDisplayFrame = 0;

    // Variables to ensure proper frame pacing.
    int frameRate;
    float framePacing;
    float framePacingTimer = 0;

    // Byte arrays to store Texture2D data for the video feeds.
    byte[] rawFrameData;
    byte[] encodedFrameData;

    // Internal resolution of the playback windows on the application's UI.
    private readonly int maxWidth = 1600;
    private readonly int maxHeight = 900;

    // Variable that dictates if the video feeds are stopped.
    bool videoPlaying = false;

    // Update is called once per frame.
    void Update()
    {
        if (videoPlaying)
        {
            framePacingTimer += Time.deltaTime;

            // Renders frames to the UI with proper frame pacing.
            if (framePacingTimer >= framePacing)
            {
                framePacingTimer = 0;

                if (RenderFrameToScene(currentDisplayFrame))
                {
                    currentDisplayFrame++;
                }
            }

        }
    }

    // Returns value of vidoPlaying variable.
    public bool GetVideoPlaying()
    {
        return this.videoPlaying;
    }

    // Returns the index of the frame currently being rendered.
    public int GetCurrentDisplayFrame()
    {
        return this.currentDisplayFrame;
    }

    // Sets the currently rendered frame to the frame with the provided index.
    public void SeekToFrame(int frameIndex)
    {
        if (frameIndex >= 0 && frameIndex < totalNumberOfFrames)
        {
            this.currentDisplayFrame = frameIndex;
            gameObject.GetComponent<FfmpegHandler>().UpdateCurrentEncodedFrameIndex(frameIndex);

            if(!videoPlaying)
            {
                RenderFrameToScene(currentDisplayFrame);
            }
        }
    }

    // Sets the totalFrames value.
    public bool SetTotalNumberOfFrames(int totalFrames)
    {
        if (totalFrames > 0)
        {
            totalNumberOfFrames = totalFrames;
            print(totalFrames);
            return true;
        }
        else
        {
            return false;
        }
    }
    // Sets the frameRate value.
    public void SetFrameRate(int frameRate)
    {
        this.frameRate = frameRate;

        if (this.frameRate > 0)
        {
            this.framePacing = 1 / (float)this.frameRate;
        }
        else
        {
            this.frameRate = 30;
            this.framePacing = (float)(1.0 / 30.0);
        }
    }

    // Initializes the Texture2D variables based on the width and height of the input .yuv file.
    public void InitializeTextures(int width, int height)
    {
        uncompressedTexture = new Texture2D(width, height);
        compressedTexture = new Texture2D(width, height);

        int rectWidth = width;
        int rectHeight = height;
        if(rectWidth > maxWidth && rectHeight > maxHeight)
        {
            while (true)
            {
                rectWidth--;
                rectHeight--;
                if((rectWidth == maxWidth && rectHeight <= maxHeight) || (rectWidth <= maxWidth && rectHeight == maxHeight))
                {
                    break;
                }
            }

            compressedFeed.gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectWidth);
            compressedFeed.gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rectHeight);
            uncompressedFeed.gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectWidth);
            uncompressedFeed.gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rectHeight);
        }
        else if (rectWidth < maxWidth && rectHeight < maxHeight)
        {
            while (true)
            {
                rectWidth++;
                rectHeight++;
                if (rectWidth == maxWidth || rectHeight == maxHeight)
                {
                    break;
                }
            }

            compressedFeed.gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectWidth);
            compressedFeed.gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rectHeight);
            uncompressedFeed.gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectWidth);
            uncompressedFeed.gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rectHeight);
        }

    }

    // Sets videoPlaying to TRUE.
    public void PlayVideo()
    {
        videoPlaying = true;
    }

    // Sets videoPlaying to FALSE.
    public void StopVideo()
    {
        videoPlaying = false;
    }

    // Renders the next frame in the sequence if the video feeds are paused.
    public void NextFrame()
    {
        if (!videoPlaying)
        {
            currentDisplayFrame++;

            if (!RenderFrameToScene(currentDisplayFrame))
            {
                currentDisplayFrame--;
            }
        }
    }

    // Renders the preceding frame in the sequence if the video feeds are paused.
    public void PreviousFrame()
    {
        if (!videoPlaying)
        {
            currentDisplayFrame--;

            if (!RenderFrameToScene(currentDisplayFrame))
            {
                currentDisplayFrame++;
            }
        }
    }

    // Renders frame to the video feed baased on the provided frame index.
    bool RenderFrameToScene(int frameIndex)
    {
        try
        {
            rawFrameData = File.ReadAllBytes(rawFramesPath + "frame" + this.currentDisplayFrame + ".png");
            encodedFrameData = File.ReadAllBytes(encodedFramesPath + "frame" + this.currentDisplayFrame + ".png");

            uncompressedTexture.LoadImage(rawFrameData);
            compressedTexture.LoadImage(encodedFrameData);

            uncompressedFeed.texture = uncompressedTexture;
            compressedFeed.texture = compressedTexture;

            return true;
        }
        catch (Exception exception)
        {
            return false;
        }
    }
}

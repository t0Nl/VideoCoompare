using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using SimpleFileBrowser;

// Class that contains all functions for user interactions with the application's UI elements.
public class UserInterfaceHandler : MonoBehaviour
{
    // References to UI elements.
    public GameObject importPanel;
    public GameObject exportPanel;
    public Slider seekSlider;
    public Text minBitrateLabel;
    public Text maxBitrateLabel;
    public Slider bitrateSlider;
    public InputField bitrateInputField;
    public Dropdown codecDropdown;
    public Dropdown containerDropdown;
    public Dropdown calculateDropdown;
    public GameObject encodingInformationPanel;

    // Reccommended bitrate variables.
    long lowestBitrate = 0;
    long mediumBitrate;
    long highestBitrate;
    private readonly double kushGaugeConstant = 0.07;

    // Variables for UI video playback.
    int totalNumberOfFrames;
    int seekFrameIndex;
    bool seekPeriodicUpdate;
    bool seekValueChangedByUser;
    bool resumePlaybackAfterSeek;
    float seekUpdatePacingTimer = 0;
    float infoUpdateTimer = 0;
    private readonly float seekUpdatePacing = 0.04167f; //24 FPS
    private readonly float oneSecondInterval = 1.0f;

    // Start is called before the first frame update.
    void Start()
    {
        // Setup FileBrowser parameters.
        FileBrowser.AddQuickLink("Users", "C:\\Users", null);
        FileBrowser.SetExcludedExtensions(".lnk", ".tmp", ".zip", ".rar", ".exe", ".jpg", ".jpeg", ".png", ".txt", ".pdf", ".py", ".cs");
    }

    // Update is called once per frame.
    void Update()
    {
        // Update the video feed seek slider.
        seekUpdatePacingTimer += Time.deltaTime;
        if (seekUpdatePacingTimer >= seekUpdatePacing)
        {
            seekUpdatePacingTimer = 0;

            PeriodicSeekSliderUpdate();
        }

        // Update the metrics display.
        infoUpdateTimer += Time.deltaTime;
        if (infoUpdateTimer >= oneSecondInterval)
        {
            infoUpdateTimer = 0;

            UpdateMetricsDisplay();
        }
    }

    // Updates the metrics display.
    void UpdateMetricsDisplay()
    {
        int specialIndex = 0;
        int numberOfIFrames = 0;
        double framesPerSecond = 0;
        double psnr = 0;
        double ssim = 0;
        double brisque = 0;
        double niqe = 0;

        List<FrameBlock> EncodedFrameBlocksIndexList = gameObject.GetComponent<FfmpegHandler>().GetEncodedFrameBlocksIndexList();
        foreach (FrameBlock fb in EncodedFrameBlocksIndexList)
        {
            numberOfIFrames += fb.numberOfIFrames;
            framesPerSecond += fb.framesEncodedPerSecond;
            psnr += fb.avgPsnr;
            ssim += fb.avgSsim;
            if(fb.avgBrisque != 0 && fb.avgNiqe != 0)
            {
                specialIndex++;
                brisque += fb.avgBrisque;
                niqe += fb.avgNiqe;
            }
        }

        framesPerSecond /= EncodedFrameBlocksIndexList.Count;
        framesPerSecond = Math.Truncate(framesPerSecond * 100) / 100;
        psnr /= EncodedFrameBlocksIndexList.Count;
        psnr = Math.Truncate(psnr * 1000) / 1000;
        ssim /= EncodedFrameBlocksIndexList.Count;
        ssim = Math.Truncate(ssim * 10000) / 10000;

        if (specialIndex != 0)
        {
            brisque /= specialIndex;
            brisque = Math.Truncate(brisque * 1000) / 1000;
            niqe /= specialIndex;
            niqe = Math.Truncate(niqe * 1000) / 1000;
        }

        encodingInformationPanel.transform.GetChild(1).GetChild(0).GetComponent<Text>().text = numberOfIFrames.ToString();
        encodingInformationPanel.transform.GetChild(2).GetChild(0).GetComponent<Text>().text = framesPerSecond.ToString();
        encodingInformationPanel.transform.GetChild(3).GetChild(0).GetComponent<Text>().text = psnr.ToString();
        encodingInformationPanel.transform.GetChild(4).GetChild(0).GetComponent<Text>().text = brisque.ToString();
        encodingInformationPanel.transform.GetChild(5).GetChild(0).GetComponent<Text>().text = ssim.ToString();
        encodingInformationPanel.transform.GetChild(6).GetChild(0).GetComponent<Text>().text = niqe.ToString();
    }

    // Listener for bitrate change slider.
    public void BitrateSliderListener(float value)
    {
        if (lowestBitrate > 0)
        {
            long desiredBitrate = Convert.ToInt64(lowestBitrate * value * 0.001);
            bitrateInputField.text = desiredBitrate.ToString();
        }
    }

    // Listener for seek slider being clicked down.
    public void SeekSliderClickDown()
    {
        if (gameObject.GetComponent<FramePlayback>().GetVideoPlaying())
        {
            resumePlaybackAfterSeek = true;
        }
        else
        {
            resumePlaybackAfterSeek = false;
        }

        gameObject.GetComponent<FramePlayback>().StopVideo();
    }

    // Listener for seek slider being clicked up.
    public void SeekSliderClickUp()
    {
        gameObject.GetComponent<FramePlayback>().SeekToFrame(seekFrameIndex);

        if (resumePlaybackAfterSeek)
        {
            gameObject.GetComponent<FramePlayback>().PlayVideo();
        }
    }

    // Listener for seek slider having its value changed.
    public void SeekSliderListener(float value)
    {
        if (!seekPeriodicUpdate)
        {
            seekFrameIndex = (int)(value * totalNumberOfFrames);
        }
        seekPeriodicUpdate = false;
    }

    // Updates the position of the seek slider based on the index of the frame being currently displayed.
    public void PeriodicSeekSliderUpdate()
    {
        if (totalNumberOfFrames > 0 && gameObject.GetComponent<FramePlayback>().GetVideoPlaying())
        {
            seekPeriodicUpdate = true;
            int currentFrame = gameObject.GetComponent<FramePlayback>().GetCurrentDisplayFrame();
            seekSlider.value = (float)currentFrame / totalNumberOfFrames;
        }
    }

    // Sets the totalNumberOfFrames variable.
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

    // Shows the Import Panel UI element.
    public void ShowImportPanel()
    {
        importPanel.gameObject.SetActive(true);
    }

    // Hides the Import Panel UI element.
    public void HideImportPanel()
    {
        importPanel.gameObject.SetActive(false);
    }

    // Shows the Export Panel UI element.
    public void ShowExportPanel()
    {
        exportPanel.gameObject.SetActive(true);
    }

    // Hides the Export Panel UI element.
    public void HideExportPanel()
    {
        exportPanel.gameObject.SetActive(false);
    }

    // Function called when the Play Button is clicked.
    public void PlayButtonFunction()
    {
        gameObject.GetComponent<FramePlayback>().PlayVideo();
    }

    // Function called when the Stop Button is clicked.
    public void StopButtonFunction()
    {
        gameObject.GetComponent<FramePlayback>().StopVideo();
    }

    // Function called when the Next Frame Button is clicked.
    public void NextFrameButtonFunction()
    {
        gameObject.GetComponent<FramePlayback>().NextFrame();
    }

    // Function called when the Previous Frame Button is clicked.
    public void PrevFrameButtonFunction()
    {
        gameObject.GetComponent<FramePlayback>().PreviousFrame();
    }

    // Function called when the Apply Button is clicked.
    public void ApplyEncodingParametersButtonFunction()
    {
        long bitrate = long.Parse(bitrateInputField.text);
        int codecIndex = codecDropdown.value;
        int containerIndex = containerDropdown.value;
        int advancedEncodingInfoIndex = calculateDropdown.value;

        gameObject.GetComponent<FfmpegHandler>().UpdateEncoderParameters(bitrate, codecIndex, containerIndex);
        gameObject.GetComponent<PythonInterface>().UpdateCalculationEnabled(advancedEncodingInfoIndex);
    }

    // Function called when the Quit Button is clicked.
    public void QuitButtonFunction()
    {
        Application.Quit();
    }

    // Function called when the Browse Button in the Import Panel is clicked.
    public void SelectFileDialog()
    {
        FileBrowser.SetDefaultFilter(".yuv");
        FileBrowser.ShowLoadDialog((paths) => {
            importPanel.transform.GetChild(0).GetChild(1).GetComponent<InputField>().text = paths[0];
        },
            () => { Debug.Log("Canceled"); },
            FileBrowser.PickMode.Files, false, null, null, "Select .yuv File", "Select");
    }

    // Function called when the Browse Button in the Export Panel is clicked.
    public void CreateFileDialog()
    {
        string desiredContainer = gameObject.GetComponent<FfmpegHandler>().GetCurrentContainerFormat();
        FileBrowser.SetDefaultFilter("." + desiredContainer);
        FileBrowser.ShowSaveDialog((paths) => {
            exportPanel.transform.GetChild(0).GetChild(1).GetComponent<InputField>().text = paths[0] + "." + desiredContainer;
            },
            () => { Debug.Log("Canceled"); },
            FileBrowser.PickMode.Files, false, null, null, "Saave Video File", "Save");
    }

    // Function that caalculates the minimum and maximum reccommended bitrates for the bitrate slider.
    private void SetRecommmendedBitrates(int width, int height, int framesPerSecond)
    {
        lowestBitrate = Convert.ToInt64(width * height * framesPerSecond * kushGaugeConstant);
        mediumBitrate = lowestBitrate * 2;
        highestBitrate = lowestBitrate * 4;

        minBitrateLabel.text = Convert.ToInt64(lowestBitrate * 0.001).ToString();
        maxBitrateLabel.text = Convert.ToInt64(highestBitrate * 0.001).ToString();

        bitrateSlider.value = 2.0f;
    }

    // Function called when the Finalize Import Button in the Import Panel is clicked.
    public void FinalizeImport()
    {
        try
        {
            string filePath = importPanel.transform.GetChild(0).GetChild(1).GetComponent<InputField>().text;

            int pixelFormatIndex = importPanel.transform.GetChild(0).GetChild(4).GetComponent<Dropdown>().value;

            int width = int.Parse(importPanel.transform.GetChild(0).GetChild(6).GetComponent<InputField>().text);

            int height = int.Parse(importPanel.transform.GetChild(0).GetChild(8).GetComponent<InputField>().text);

            int framesPerSecond = int.Parse(importPanel.transform.GetChild(0).GetChild(10).GetComponent<InputField>().text);

            SetRecommmendedBitrates(width, height, framesPerSecond);

            gameObject.GetComponent<FfmpegHandler>().SetEncodingParameters(filePath, pixelFormatIndex, width, height, framesPerSecond, (int) mediumBitrate);

            gameObject.GetComponent<FramePlayback>().InitializeTextures(width, height);
            gameObject.GetComponent<FramePlayback>().SetFrameRate(framesPerSecond);

            HideImportPanel();
            codecDropdown.value = 0;
            containerDropdown.value = 0;

            gameObject.GetComponent<FramePlayback>().PlayVideo();
        }
        catch (Exception e)
        {
            importPanel.transform.GetChild(0).GetChild(11).gameObject.SetActive(true);
        }
    }

    // Function called when the Finalize Export Button in the Export Panel is clicked.
    public void FinalizeExport()
    {
        string filePath = exportPanel.transform.GetChild(0).GetChild(1).GetComponent<InputField>().text;
        gameObject.GetComponent<FfmpegHandler>().ExportVideo(filePath);
        HideExportPanel();
    }
}

using System;
using System.Text;
using Microsoft.ML.OnnxRuntime.Unity;
using Microsoft.ML.OnnxRuntime.Examples;
using TextureSource;
using UnityEngine;

/// <summary>
/// RT-DETRv2: RT-DETRv2 Beat YOLOs on Real-time Object Detection
/// 
/// Licensed under Apache License 2.0
/// See the original source code at:
/// https://github.com/lyuwenyu/RT-DETR
/// </summary>
[RequireComponent(typeof(VirtualTextureSource))]
public class RtDetrSample : MonoBehaviour
{
    [SerializeField]
    private OrtAsset model;

    [SerializeField]
    private RemoteFile modelFile = new("https://github.com/asus4/onnxruntime-unity-examples/releases/download/v0.2.7/rtdetrv2_r18vd_120e_coco_rerun.onnx");

    [SerializeField]
    private RtDetr.Options options;

    [Header("Visualization Options")]
    [SerializeField]
    private TMPro.TMP_Text detectionBoxPrefab;

    [SerializeField]
    private RectTransform detectionContainer;

    [SerializeField]
    private int maxDetections = 20;

    private RtDetr inference;
    private TMPro.TMP_Text[] detectionBoxes;
    private readonly StringBuilder sb = new();

    private async void Start()
    {
        byte[] onnxFile = model != null
            ? model.bytes
            : await modelFile.Load(destroyCancellationToken);
        inference = new RtDetr(onnxFile, options);

        detectionBoxes = new TMPro.TMP_Text[maxDetections];
        for (int i = 0; i < maxDetections; i++)
        {
            var box = Instantiate(detectionBoxPrefab, detectionContainer);
            box.name = $"Detection {i}";
            box.gameObject.SetActive(false);
            detectionBoxes[i] = box;
        }

        if (TryGetComponent(out VirtualTextureSource source))
        {
            source.OnTexture.AddListener(OnTexture);
        }
    }

    private void OnDestroy()
    {
        if (TryGetComponent(out VirtualTextureSource source))
        {
            source.OnTexture.RemoveListener(OnTexture);
        }
        inference?.Dispose();
    }

    private void OnTexture(Texture texture)
    {
        if (inference == null)
        {
            return;
        }

        inference.Run(texture);

        UpdateDetectionBox(inference.Detections);
    }

    private void UpdateDetectionBox(ReadOnlySpan<RtDetr.Detection> detections)
    {
        var labels = inference.labelNames;
        Vector2 viewportSize = detectionContainer.rect.size;

        int i;
        int length = Math.Min(detections.Length, maxDetections);
        for (i = 0; i < length; i++)
        {
            var detection = detections[i];
            string label = labels[detection.label];

            var box = detectionBoxes[i];
            box.gameObject.SetActive(true);

            // Using StringBuilder to reduce GC
            sb.Clear();
            sb.Append(label);
            sb.Append(": ");
            sb.Append((int)(detection.probability * 100));
            sb.Append('%');
            box.SetText(sb);

            // The detection rect is model space
            // Needs to be converted to viewport space
            RectTransform rt = box.rectTransform;
            Rect rect = inference.ConvertToViewport(detection.rect);
            rt.anchoredPosition = rect.min * viewportSize;
            rt.sizeDelta = rect.size * viewportSize;
        }
        // Hide unused boxes
        for (; i < maxDetections; i++)
        {
            detectionBoxes[i].gameObject.SetActive(false);
        }
    }
}

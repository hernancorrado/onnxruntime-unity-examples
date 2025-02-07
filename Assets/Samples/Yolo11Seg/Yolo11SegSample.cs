using System;
using System.Text;
using Microsoft.ML.OnnxRuntime.Unity;
using Microsoft.ML.OnnxRuntime.Examples;
using TextureSource;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(VirtualTextureSource))]
public class Yolo11SegSample : MonoBehaviour
{
    [SerializeField]
    private OrtAsset model;

    [SerializeField]
    private Yolo11Seg.Options options;

    [Header("Visualization Options")]
    [SerializeField]
    private TMPro.TMP_Text detectionBoxPrefab;

    [SerializeField]
    private RectTransform detectionContainer;

    [SerializeField]
    private int maxDetections = 20;

    [SerializeField]
    private RawImage segmentationImage;

    private Yolo11Seg inference;
    private TMPro.TMP_Text[] detectionBoxes;
    private Image[] detectionBoxOutline;
    private readonly StringBuilder sb = new();

    private void Start()
    {
        inference = new Yolo11Seg(model.bytes, options);

        detectionBoxes = new TMPro.TMP_Text[maxDetections];
        detectionBoxOutline = new Image[maxDetections];
        for (int i = 0; i < maxDetections; i++)
        {
            var box = Instantiate(detectionBoxPrefab, detectionContainer);
            box.name = $"Detection {i}";
            box.gameObject.SetActive(false);
            detectionBoxes[i] = box;
            detectionBoxOutline[i] = box.transform.GetChild(0).GetComponent<Image>();
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

    public void OnTexture(Texture texture)
    {
        if (inference == null)
        {
            return;
        }

        inference.Run(texture);

        UpdateDetectionBox(inference.Detections);
        segmentationImage.texture = inference.SegmentationTexture;
    }

    private void UpdateDetectionBox(ReadOnlySpan<Yolo11Seg.Detection> detections)
    {
        var labels = inference.labelNames;
        Vector2 viewportSize = detectionContainer.rect.size;

        int i;
        int length = Math.Min(detections.Length, maxDetections);
        for (i = 0; i < length; i++)
        {
            var detection = detections[i];

            var color = detection.GetColor();

            var box = detectionBoxes[i];
            box.gameObject.SetActive(true);

            // Using StringBuilder to reduce GC
            sb.Clear();
            sb.Append(labels[detection.label]);
            sb.Append(": ");
            sb.Append((int)(detection.probability * 100));
            sb.Append('%');
            box.SetText(sb);
            box.color = color;

            // The detection rect is model space
            // Needs to be converted to viewport space
            RectTransform rt = box.rectTransform;
            Rect rect = inference.ConvertToViewport(detection.rect);
            rt.anchoredPosition = rect.min * viewportSize;
            rt.sizeDelta = rect.size * viewportSize;

            detectionBoxOutline[i].color = color;
        }

        // Hide unused boxes
        for (; i < maxDetections; i++)
        {
            detectionBoxes[i].gameObject.SetActive(false);
        }
    }
}

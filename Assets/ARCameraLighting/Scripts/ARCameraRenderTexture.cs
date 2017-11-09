﻿using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Vuforia;

/**
 * Captures the AR camera feed as a RenderTexture. 
 * Optional downsize and blue the texture.
 */
[RequireComponent(typeof(FastBlur))]
public class ARCameraRenderTexture : MonoBehaviour
{
    public RenderTexture targetRenderTexture;

    // Optionally blur the render tex
    public bool shouldBlurRenderTexture;

    // Are we capturing the camera feed?
    public bool IsCapturing { get { return isCapturing; } }

	//private IARCamera cameraBlit;
    private int workingRenderTextureID = Shader.PropertyToID("_ARCameraRenderTexture");
    private CommandBuffer m_blitCommandBuffer;
    private CommandBuffer m_releaseCommandBuffer;
    private FastBlur fastBlur;
    private Material blendMaterial = null;
    private bool isCapturing;

    IEnumerator Start()
    {
        Debug.Assert(targetRenderTexture, "Please assign a target RenderTexture");

        blendMaterial = Resources.Load<Material>("Materials/Blend");
        Debug.Assert(blendMaterial);




        // Wait for the AR session to start
        while (!VuforiaARController.Instance.HasStarted )
        {
           //yield return null;
           yield return new WaitForSeconds(1);
        }

        SetupBackgroundBlit();
    }

    

    private void SetupBackgroundBlit()
    {
        var mainCamera = GetComponent<Camera>();
		//IARCamera arCamera = GetComponent<ARCamera>().Camera;
		//Debug.Assert (arCamera!=null);

        int renderTextureWidth = targetRenderTexture.width;
        int renderTextureHeight = targetRenderTexture.height;

        // Clean up any previous command buffer and events hooks
        if (m_blitCommandBuffer != null)
        {
            ARResources.DeregisterChangeCallback(SetupBackgroundBlit);
			mainCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, m_blitCommandBuffer);
            mainCamera.RemoveCommandBuffer(CameraEvent.AfterSkybox, m_releaseCommandBuffer);
        }

        // Create the blit command buffer
        m_blitCommandBuffer = new CommandBuffer();
        m_blitCommandBuffer.GetTemporaryRT(workingRenderTextureID, renderTextureWidth, renderTextureHeight, 0, FilterMode.Bilinear);
        m_blitCommandBuffer.name = "Get ARBackground";

        var blitMat = Resources.Load<Material>("Materials/ARCoreBlit");
        var texture = this.GetComponentInChildren<BackgroundPlaneBehaviour>().Material.mainTexture;
        m_blitCommandBuffer.Blit(texture, workingRenderTextureID, blitMat); //this process may not be necesary

        if (shouldBlurRenderTexture)
        {
            // The optional blur step
            fastBlur = GetComponent<FastBlur>();
            Debug.Assert(fastBlur);
            fastBlur.CreateBlurCommandBuffer(m_blitCommandBuffer, workingRenderTextureID, renderTextureWidth, renderTextureHeight);
        }

        // Copy over to the target texture. Use a blend texture to stop light flickering.
        m_blitCommandBuffer.Blit(workingRenderTextureID, targetRenderTexture, blendMaterial);

        // Run the command buffer just before opaque rendering
        mainCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, m_blitCommandBuffer);

        // Cleanup the temp render textures
        m_releaseCommandBuffer = new CommandBuffer();
        m_releaseCommandBuffer.name = "Release ARBackground";
        m_releaseCommandBuffer.ReleaseTemporaryRT(workingRenderTextureID);
        mainCamera.AddCommandBuffer(CameraEvent.AfterSkybox, m_releaseCommandBuffer);

        isCapturing = true;

        // Rebuild the command buffer if anything changes
        ARResources.RegisterChangeCallback(SetupBackgroundBlit);
    }


}

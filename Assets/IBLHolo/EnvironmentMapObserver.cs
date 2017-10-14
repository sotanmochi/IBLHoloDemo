// Copyright (c) 2017 sotan
// Licensed under the MIT License.
// See LICENSE in the project root for license information.

#define SKYSHOP

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
#if SKYSHOP
using mset;
#endif

public enum IBLTypes
{
	ReflectionProbe = 0,
	Skyshop = 1
}

public class EnvironmentMapObserver : MonoBehaviour 
{
	public GameObject DefaultEnvironmentMapSphere;

	public string URL = "http://192.168.13.81:8080/?action=snapshot";

	public GameObject EnvironmentMapSphere;

    public float TimeBetweenUpdates = 0.5f;
	private float updateTime = 0;

	public IBLTypes IBLType = IBLTypes.ReflectionProbe;

	public ReflectionProbe ReflectionProbe;
#if SKYSHOP
	public Cubemap EnvironmentCubemap;
	public mset.Sky Sky;
#endif

	private bool observerIsRunning = false;

	private Renderer targetRenderer;
	private Texture2D texture;
	private List<byte> imageBytes;

	private Camera mainCamera;

	IEnumerator Start()
	{
		observerIsRunning = false;
		EnvironmentMapSphere.SetActive(false);

		UnityWebRequest request = UnityWebRequest.GetTexture(URL);
		request.timeout = 3;
		yield return request.Send();
		if (request.responseCode != 200)
		{
			switch(IBLType)
			{
				case IBLTypes.ReflectionProbe:
					ReflectionProbe.RenderProbe();
					break;
				case IBLTypes.Skyshop:
					RenderToCubemap();
					UpdateSphericalHarmonics();
					break;
				default:
					break;
			}
			yield break;
		}

		DefaultEnvironmentMapSphere.SetActive(false);
		EnvironmentMapSphere.SetActive(true);
		targetRenderer = EnvironmentMapSphere.GetComponent<Renderer>();

		texture = new Texture2D(2, 2);
		imageBytes = new List<byte>();

		mainCamera = Camera.main;

		if ((IBLType == IBLTypes.ReflectionProbe) && (ReflectionProbe != null))
		{
			observerIsRunning = true;
		}
#if SKYSHOP
		else if ((IBLType == IBLTypes.Skyshop) && (EnvironmentCubemap != null) && (Sky != null))
		{
			observerIsRunning = true;
		}
#endif
	}

	void Update()
	{
		if (observerIsRunning && (Time.time - updateTime) >= TimeBetweenUpdates)
		{
			StartCoroutine(UpdateEnvironmentMapRoutine());
			updateTime = Time.time;
		}
	}

	private IEnumerator UpdateEnvironmentMapRoutine()
	{
		UnityWebRequest request = UnityWebRequest.GetTexture(URL);
		yield return request.Send();

		if (request.responseCode == 200)
		{
			UpdateObserverRotation();
			UpdateTexture(request.downloadHandler.data);
			switch(IBLType)
			{
				case IBLTypes.ReflectionProbe:
					ReflectionProbe.RenderProbe();
					break;
				case IBLTypes.Skyshop:
					RenderToCubemap();
					UpdateSphericalHarmonics();
					break;
				default:
					break;
			}
			Resources.UnloadUnusedAssets();
		}
		else if (request.isError)
		{
			Debug.Log(request.error);
		}
	}

	private void UpdateObserverRotation()
	{
		Transform transform = mainCamera.transform;
		Vector3 cameraRotation = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, transform.eulerAngles.z);
		this.transform.eulerAngles = new Vector3(cameraRotation.x, cameraRotation.y, cameraRotation.z);
	}

	/**
	 * Update texture for Environment Map Sphere.
	 */
	private void UpdateTexture(byte[] data)
	{
		imageBytes.Clear();
		bool isLoadStart = false;
		byte oldByte = 0;

		for(int i = 0; i < data.Length; i++)
		{
			byte byteData = data[i];

			if (!isLoadStart) {
				// mjpeg start ( [0xFF 0xD8 ... )
				if (oldByte == 0xFF)
				{
					imageBytes.Add(0xFF);
				}
				if (byteData == 0xD8)
				{
					imageBytes.Add(0xD8);
					isLoadStart = true;
				}
			}
			else
			{
				imageBytes.Add(byteData);

				// mjpeg end (... 0xFF 0xD9] )
				if (oldByte == 0xFF && byteData == 0xD9)
				{
					texture.LoadImage((byte[])imageBytes.ToArray());
					imageBytes.Clear();
					isLoadStart = false;
				}
			}

			oldByte = byteData;
		}

		targetRenderer.material.mainTexture = texture;
	}

	/**
	 * Update cubemap for Specular IBL Shader.
	 */
	private void RenderToCubemap()
	{
#if SKYSHOP

		// Create temporary camera for rendering.
		GameObject go = new GameObject("CubemapCamera");
		go.AddComponent<Camera>();
		go.GetComponent<Camera>().transform.position = this.transform.position;
		go.GetComponent<Camera>().transform.rotation = Quaternion.identity;

		// Render into cubemap.
		go.GetComponent<Camera>().RenderToCubemap(EnvironmentCubemap);

		// Destroy temporary camera.
		DestroyImmediate(go);
#endif
	}

	/**
	 * Update Spherical Harmonics for Diffuse IBL Shader.
	 */
	private void UpdateSphericalHarmonics()
	{
#if SKYSHOP
		if(Sky.SpecularCube as Cubemap) {
			mset.SHUtil.projectCube(ref Sky.SH, Sky.SpecularCube as Cubemap, 0, false);
			mset.SHUtil.convolve( ref Sky.SH );
			Sky.SH.copyToBuffer();
			Sky.Dirty = true;
		}
#endif
	}
}

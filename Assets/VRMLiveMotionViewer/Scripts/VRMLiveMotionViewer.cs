using System.Collections;
using System.IO;
using UnityEngine;

#if !UNITY_EDITOR && UNITY_WSA_10_0
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
#endif

using HoloToolkit.Unity.InputModule;
using Photon.Pun;
using Photon.Realtime;

public class VRMLiveMotionViewer : MonoBehaviourPunCallbacks, IOnEventCallback
{
	public int SerializationRate = 30;
	public GameObject VRMRoot;

	public UniHumanoid.HumanPoseTransfer m_target;
	public UniHumanoid.HumanPoseTransfer m_source;

	public OVRLipSyncContext lipSyncContext;

    void Awake()
    {
        // Defines how many times per second OnPhotonSerialize should be called on PhotonViews.
        PhotonNetwork.SendRate = 2 * SerializationRate;
        PhotonNetwork.SerializationRate = SerializationRate;
		Debug.LogFormat("PhotonNetwork.SendRate: {0}", PhotonNetwork.SendRate);
		Debug.LogFormat("PhotonNetwork.SerializationRate: {0}", PhotonNetwork.SerializationRate);
    }

    public void OnEvent(ExitGames.Client.Photon.EventData photonEvent)
    {
		if (photonEvent.Code == (byte)VRMLiveMotionEventCode.SetHumanPoseTransferSource)
        {
			Debug.Log("OnEvent: EventCode is SetHumanPoseTransferSource");

			int receivedViewID = (int)photonEvent.Parameters[ParameterCode.Data];

			GameObject humanPoseSynchronizer = PhotonView.Find(receivedViewID).gameObject;
            m_source = humanPoseSynchronizer.GetComponent<UniHumanoid.HumanPoseTransfer>();
			humanPoseSynchronizer.GetComponent<Renderer>().enabled = false;

			SetupTarget();
		}

		if (photonEvent.Code == (byte)VRMLiveMotionEventCode.SetLipSync)
		{
            Debug.Log("OnEvent: EventCode is SetLipSync.");
            int receivedViewID = (int)photonEvent.Parameters[ParameterCode.Data];

			GameObject photonVoiceSpeaker = PhotonView.Find(receivedViewID).gameObject;
            lipSyncContext = photonVoiceSpeaker.AddComponent<OVRLipSyncContext>();
			lipSyncContext.audioMute = false;

            var morphTarget = VRMRoot.GetComponentInChildren<VRMLipSyncMorphTarget>();
			if(morphTarget != null)
			{
	            morphTarget.lipsyncContext = lipSyncContext;
			}
		}
	}

	public void UpdateVRMRootPosition(RaycastHit hitInfo)
	{
		if(Vector3.Dot(hitInfo.normal, Vector3.up) > 0.95f)
		{
			VRMRoot.transform.position = hitInfo.point;

			Vector3 direction = Camera.main.transform.position - hitInfo.point;
			direction = Vector3.Cross(Vector3.Cross(transform.up, direction), transform.up); // up方向成分の除去
			VRMRoot.transform.forward = direction;
		}
	}

	public void LoadVrmUsingFilePicker()
    {
#if !UNITY_EDITOR && UNITY_WSA_10_0

		UnityEngine.WSA.Application.InvokeOnUIThread(async () =>
		{
			var openPicker = new FileOpenPicker();
			openPicker.SuggestedStartLocation = PickerLocationId.Objects3D;
			openPicker.FileTypeFilter.Add(".vrm");

			var file = await openPicker.PickSingleFileAsync();
			UnityEngine.WSA.Application.InvokeOnAppThread(() => 
			{
				if (file != null)
				{
					StartCoroutine(LoadVrmCoroutine(file.Path));
				}
			}, false);
		}, false);

#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

		var path = VRM.FileDialogForWindows.FileDialog("open VRM", ".vrm");
		if (!string.IsNullOrEmpty(path))
		{
			StartCoroutine(LoadVrmCoroutine(path));
		}

#endif
	}

	private IEnumerator LoadVrmCoroutine(string path)
	{
		var www = new WWW("file://" + path);
		yield return www;
		VRM.VRMImporter.LoadVrmAsync(www.bytes, OnLoadedVrm);
	}

	private void OnLoadedVrm(GameObject vrm)
	{
		if (VRMRoot != null)
		{
			vrm.transform.SetParent(VRMRoot.transform, false);

            var humanPoseTransfer = vrm.AddComponent<UniHumanoid.HumanPoseTransfer>();
            if (m_target != null)
            {
                GameObject.Destroy(m_target.gameObject);
            }
            m_target = humanPoseTransfer;

			var morphTarget = vrm.AddComponent<VRMLipSyncMorphTarget>();
			morphTarget.blendShapeProxy = vrm.GetComponent<VRM.VRMBlendShapeProxy>();
            morphTarget.lipsyncContext = lipSyncContext;

			SetupTarget();
		}
	}

	private void SetupTarget()
	{
		if (m_target != null)
		{
			m_target.Source = m_source;
			m_target.SourceType = UniHumanoid.HumanPoseTransfer.HumanPoseTransferSourceType.HumanPoseTransfer;
		}
	}

	public void LoadBvhUsingFilePicker()
    {
#if !UNITY_EDITOR && UNITY_WSA_10_0

		UnityEngine.WSA.Application.InvokeOnUIThread(async () =>
		{
			var openPicker = new FileOpenPicker();
			openPicker.SuggestedStartLocation = PickerLocationId.Objects3D;
			openPicker.FileTypeFilter.Add(".bvh");

			var file = await openPicker.PickSingleFileAsync();
			UnityEngine.WSA.Application.InvokeOnAppThread(() => 
			{
				if(file != null)
				{
					StartCoroutine(LoadBvhCoroutine(file.Path));
				}
			}, false);
		}, false);

#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

		var path = VRM.FileDialogForWindows.FileDialog("open BVH", ".bvh");
		if (!string.IsNullOrEmpty(path))
		{
			StartCoroutine(LoadBvhCoroutine(path));
		}

#endif
	}

	IEnumerator LoadBvhCoroutine(string path)
	{
		var www = new WWW("file://" + path);
		yield return www;

		var context = new UniHumanoid.ImporterContext
		{
			Path = path,
			Source = www.text
		};
		ModifiedBvhImporter.Import(context);

		if (m_source != null)
		{
			GameObject.Destroy(m_source.gameObject);
		}
		m_source = context.Root.GetComponent<UniHumanoid.HumanPoseTransfer>();

        m_source.GetComponent<Renderer>().enabled = false;

		SetupTarget();
	}
}

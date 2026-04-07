using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceGraphicsToolkit.LightAndShadow
{
	/// <summary>This component allows you to calculate the 0..1 line of sight between two points based on raycast and manual opacity calculations through volumetric objects.</summary>
	[ExecuteInEditMode]
	[RequireComponent(typeof(Light))]
	[HelpURL(SgtCommon.HelpUrlPrefix + "SgtLightOccluder")]
	[AddComponentMenu(SgtCommon.ComponentMenuPrefix + "Light Occluder")]
	public class SgtLightOccluder : MonoBehaviour
	{
		/// <summary>The layers that will be sampled when calculating the occlusion.</summary>
		public LayerMask Layers { set { layers = value; } get { return layers; } } [SerializeField] private LayerMask layers = Physics.DefaultRaycastLayers;

		/// <summary>This allows you to set the maximum scale when there is no occlusion.</summary>
		public Vector3 MaxScale { set { maxScale = value; } get { return maxScale; } } [SerializeField] private Vector3 maxScale = Vector3.one;

		/// <summary>This allows you to set the maximum scale when there is no occlusion.</summary>
		public float MaxScaleMultiplier { set { maxScaleMultiplier = value; } get { return maxScaleMultiplier; } } [SerializeField] private float maxScaleMultiplier = 1.0f;

		/// <summary>The occlusion will be calculated from this point.
		/// None/null = Main Camera.</summary>
		public Transform Observer { set { observer = value; } get { return observer; } } [SerializeField] private Transform observer;

		public interface IOccluder
		{
			public float CalculateOcclusion(int layers, Vector3 worldEye, Vector3 worldTgt);
		}

		private static RaycastHit[] tempHits = new RaycastHit[1024];

		private static HashSet<IOccluder> occluders = new HashSet<IOccluder>();

		public static void Register(IOccluder occluder)
		{
			occluders.Add(occluder);
		}

		public static void Unregister(IOccluder occluder)
		{
			occluders.Remove(occluder);
		}

		/// <summary>This will calculate how much light is blocked between the eye and tgt world space points for the specified layer.</summary>
		public static float CalculateOcclusion(int layers, Vector3 worldEye, Vector3 worldTgt)
		{
			var occlusion = CalculateOcclusionRaycast(layers, worldEye, worldTgt);

			if (occlusion < 1.0f)
			{
				foreach (var occluder in occluders)
				{
					occlusion = Mathf.Lerp(occlusion, 1.0f, occluder.CalculateOcclusion(layers, worldEye, worldTgt));

					if (occlusion >= 0.999f)
					{
						occlusion = 1.0f;

						break;
					}
				}
			}

			return occlusion;
		}

		protected virtual void LateUpdate()
		{
			if (observer != null)
			{
				DoScale(observer);
			}
			else if (Camera.main != null)
			{
				DoScale(Camera.main.transform);
			}
		}

		private void DoScale(Transform eye)
		{
			transform.localScale = maxScale * maxScaleMultiplier * (1.0f - CalculateOcclusion(layers, eye.position, transform.position));
		}

		private static float CalculateOcclusionRaycast(int layers, Vector3 eye, Vector3 tgt)
		{
			var direction = Vector3.Normalize(tgt - eye);
			var distance  = Vector3.Magnitude(tgt - eye);
			var hitCount  = Physics.RaycastNonAlloc(eye, direction, tempHits, distance, layers, QueryTriggerInteraction.Ignore);

			return hitCount > 0 ? 1.0f : 0.0f;
		}
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.LightAndShadow
{
	using UnityEditor;

	[CanEditMultipleObjects]
	[CustomEditor(typeof(SgtLightOccluder))]
	public class SgtLightOccluder_Editor : CW.Common.CwEditor
	{
		protected override void OnInspector()
		{
			SgtLightOccluder tgt; SgtLightOccluder[] tgts; GetTargets(out tgt, out tgts);

			Draw("layers", "The layers that will be sampled when calculating the occlusion.");
			Draw("maxScale", "This allows you to set the maximum scale when there is no occlusion.");
			Draw("maxScaleMultiplier", "This allows you to set the maximum scale when there is no occlusion.");
			Draw("observer", "The occlusion will be calculated from this point.\n\nNone/null = Main Camera.");
		}
	}
}
#endif
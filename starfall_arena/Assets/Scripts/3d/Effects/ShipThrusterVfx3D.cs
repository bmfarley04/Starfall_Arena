using System.Collections.Generic;
using UnityEngine;

public class ShipThrusterVfx3D : MonoBehaviour
{
    private struct ThrusterParticleState
    {
        public ParticleSystem.MinMaxCurve emissionRate;
        public ParticleSystem.MinMaxCurve startSpeed;
        public ParticleSystem.MinMaxCurve startLifetime;
        public Color startColor;
    }

    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private ThrusterEffects3DConfig thrusterEffects;

    private readonly Dictionary<ParticleSystem, ThrusterParticleState> _thrusterOriginalStates = new();
    private float _currentThrusterIntensity;

    private void Awake()
    {
        if (shipFlight == null)
        {
            shipFlight = GetComponent<ShipFlight3D>();
        }

        CacheThrusterDefaults();
    }

    private void Update()
    {
        UpdateThrusters();
    }

    public void SetShipFlight(ShipFlight3D flight)
    {
        shipFlight = flight;
    }

    public void SetThrusterEffects(ThrusterEffects3DConfig config)
    {
        thrusterEffects = config;
        CacheThrusterDefaults();
    }

    private void CacheThrusterDefaults()
    {
        _thrusterOriginalStates.Clear();

        if (thrusterEffects.thrusters == null)
        {
            return;
        }

        foreach (ParticleSystem thruster in thrusterEffects.thrusters)
        {
            if (thruster == null || _thrusterOriginalStates.ContainsKey(thruster))
            {
                continue;
            }

            var emission = thruster.emission;
            var main = thruster.main;

            _thrusterOriginalStates[thruster] = new ThrusterParticleState
            {
                emissionRate = emission.rateOverTime,
                startSpeed = main.startSpeed,
                startLifetime = main.startLifetime,
                startColor = main.startColor.color
            };

            emission.rateOverTime = ScaleCurve(_thrusterOriginalStates[thruster].emissionRate, 0f);
            main.startSpeed = ScaleCurve(_thrusterOriginalStates[thruster].startSpeed, 0f);
            main.startLifetime = ScaleCurve(_thrusterOriginalStates[thruster].startLifetime, 0f);
            main.startColor = thrusterEffects.invertColors
                ? InvertColor(_thrusterOriginalStates[thruster].startColor)
                : _thrusterOriginalStates[thruster].startColor;

            thruster.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void UpdateThrusters()
    {
        if (shipFlight == null || Time.deltaTime <= 0f)
        {
            return;
        }

        if (thrusterEffects.thrusters == null || thrusterEffects.thrusters.Count == 0)
        {
            return;
        }

        float targetIntensity = shipFlight.IsApplyingThrust ? 1f : 0f;
        float rampStep = thrusterEffects.rampTime > 0f ? Time.deltaTime / thrusterEffects.rampTime : 1f;
        _currentThrusterIntensity = Mathf.MoveTowards(_currentThrusterIntensity, targetIntensity, rampStep);

        foreach (ParticleSystem thruster in thrusterEffects.thrusters)
        {
            if (thruster == null || !_thrusterOriginalStates.TryGetValue(thruster, out ThrusterParticleState originalState))
            {
                continue;
            }

            if (_currentThrusterIntensity > 0f)
            {
                if (!thruster.isPlaying)
                {
                    thruster.Play();
                }
            }
            else if (thruster.isPlaying)
            {
                thruster.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            var emission = thruster.emission;
            var main = thruster.main;

            emission.rateOverTime = ScaleCurve(originalState.emissionRate, _currentThrusterIntensity);
            main.startSpeed = ScaleCurve(originalState.startSpeed, _currentThrusterIntensity);
            main.startLifetime = ScaleCurve(originalState.startLifetime, _currentThrusterIntensity);
            main.startColor = thrusterEffects.invertColors
                ? InvertColor(originalState.startColor)
                : originalState.startColor;
        }
    }

    private static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve curve, float scale)
    {
        ParticleSystem.MinMaxCurve scaledCurve = curve;
        scaledCurve.constant *= scale;
        scaledCurve.constantMin *= scale;
        scaledCurve.constantMax *= scale;
        scaledCurve.curveMultiplier *= scale;
        return scaledCurve;
    }

    private static Color InvertColor(Color color)
    {
        return new Color(1f - color.r, 1f - color.g, 1f - color.b, color.a);
    }
}

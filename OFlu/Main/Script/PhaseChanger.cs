using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;

public class PhaseChanger : MonoBehaviour
{
    public float heat = 0.1f;
    public float cooling = 0.1f;
    public ObiCollider hotCollider_0 = null;
    public ObiCollider hotCollider_1 = null;
    public ObiCollider coldCollider_0 = null;
    public ObiCollider coldCollider_1 = null;

    private ObiSolver solver;

    public float min = 0f;
    public float max = 5f;
    public Gradient grad;

    void Awake()
    {
        solver = GameObject.Find("Solver").GetComponent<Obi.ObiSolver>();

        _emitter = GameObject.Find("Solver/Emitter").GetComponent<Obi.ObiEmitter>();
        _emitter.OnEmitParticle += Emitter_OnEmitParticle;

        _barInput = GameObject.Find("Framework").GetComponent<BarInput>();
    }

    void OnEnable()
    {
        solver.OnCollision += Solver_OnCollision;
    }

    void OnDisable()
    {
        solver.OnCollision -= Solver_OnCollision;
    }

    void Solver_OnCollision(object sender, Obi.ObiSolver.ObiCollisionEventArgs e)
    {
        if (_barInput.FluidType != BarInput.EFluidType.Type3)
        {
            return;
        }

        var colliderWorld = ObiColliderWorld.GetInstance();

        for (int i = 0; i < e.contacts.Count; ++i)
        {
            if (e.contacts.Data[i].distance < 0.001f)
            {
                var col = colliderWorld.colliderHandles[e.contacts.Data[i].bodyB].owner;
                if (col != null)
                {
                    int k = e.contacts.Data[i].bodyA;

                    Vector4 userData = solver.userData[k];
                    if (col == coldCollider_0 || col == coldCollider_1)
                    {
                        userData[0] = Mathf.Min(10, userData[0] + cooling * Time.fixedDeltaTime);
                        userData[0] = 5f; // viscosity
                        userData[1] = 2f; // smoothing
                        userData[2] = 1f; // surfaceTension
                        userData[3] = 20f; // atmosphericDrag
                    }
                    else if (col == hotCollider_0 || col == hotCollider_1)
                    {
                        userData[0] = Mathf.Max(0.05f, userData[0] - heat * Time.fixedDeltaTime);
                        userData[0] = 0f; // viscosity
                        userData[1] = 2.5f; // smoothing
                        userData[2] = 0.5f; // surfaceTension
                        userData[3] = 0f; // atmosphericDrag
                    }
                    solver.userData[k] = userData;
                }
            }
        }
    }

    void Emitter_OnEmitParticle(ObiEmitter emitter, int particleIndex)
    {
        if (_barInput.FluidType != BarInput.EFluidType.Type3)
        {
            return;
        }

        if (emitter.solver != null)
        {
            int k = emitter.solverIndices[particleIndex];

            Vector4 userData = emitter.solver.userData[k];
            userData[0] = emitter.solver.viscosities[k];
            //userData[1] = emitter.solver.smoothingRadii[k];
            userData[2] = emitter.solver.surfaceTension[k];
            userData[3] = emitter.solver.atmosphericDrag[k];
            emitter.solver.userData[k] = userData;
        }
    }

    void LateUpdate()
    {
        if (_barInput.FluidType != BarInput.EFluidType.Type3)
        {
            for (int i = 0; i < _emitter.solverIndices.Length; ++i)
            {
                int k = _emitter.solverIndices[i];
                _emitter.solver.colors[k] = grad.Evaluate(0f);
            }
            return;
        }


        for (int i = 0; i < _emitter.solverIndices.Length; ++i)
        {
            int k = _emitter.solverIndices[i];
            _emitter.solver.viscosities[k] = _emitter.solver.userData[k][0];
            //_emitter.solver.smoothingRadii[k] = _emitter.solver.userData[k][1] / (1f / (10 * Mathf.Pow(0.3f, 1 / 3f)));
            _emitter.solver.surfaceTension[k] = _emitter.solver.userData[k][2];
            _emitter.solver.atmosphericDrag[k] = _emitter.solver.userData[k][3];
            _emitter.solver.colors[k] = grad.Evaluate((_emitter.solver.viscosities[k] - min) / (max - min));
        }
    }

    private Obi.ObiEmitter _emitter;
    private BarInput _barInput;

}

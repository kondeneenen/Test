using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarInput : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 60;

        _barL = GameObject.Find("BarL");
        _barR = GameObject.Find("BarR");
        _barLUnder = _barL.transform.Find("BarLUnder").gameObject;
        _barRUnder = _barR.transform.Find("BarRUnder").gameObject;
        _colorBlue = _barL.GetComponent<Renderer>().material.color;
        _colorRed = _barR.GetComponent<Renderer>().material.color;

        _mainCamera = Camera.main;

        _emitter = GameObject.Find("Solver/Emitter").GetComponent<Obi.ObiEmitter>();
        _text = GameObject.Find("Canvas/Text").GetComponent<UnityEngine.UI.Text>();

        _phaseChanger = GameObject.Find("Framework").GetComponent<PhaseChanger>();

        _fluidType = EFluidType.Type0;
        setFluidMaterial();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.A))
        {
            _barL.transform.position += Vector3.right * -barSpeed * Time.deltaTime;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            _barL.transform.position += Vector3.right * barSpeed * Time.deltaTime;
        }
        else if (Input.GetKey(KeyCode.W))
        {
            _barL.transform.position += Vector3.forward * barSpeed * Time.deltaTime;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            _barL.transform.position += Vector3.forward * -barSpeed * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            _barR.transform.position += Vector3.right * -barSpeed * Time.deltaTime;
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            _barR.transform.position += Vector3.right * barSpeed * Time.deltaTime;
        }
        else if (Input.GetKey(KeyCode.UpArrow))
        {
            _barR.transform.position += Vector3.forward * barSpeed * Time.deltaTime;
        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            _barR.transform.position += Vector3.forward * -barSpeed * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.Alpha1))
        {
            _barL.transform.rotation *= Quaternion.AngleAxis(angularSpeed * Time.deltaTime, Vector3.up);
        }
        else if (Input.GetKey(KeyCode.Alpha2))
        {
            _barL.transform.rotation *= Quaternion.AngleAxis(-angularSpeed * Time.deltaTime, Vector3.up);
        }

        if (Input.GetKey(KeyCode.Alpha9))
        {
            _barR.transform.rotation *= Quaternion.AngleAxis(angularSpeed * Time.deltaTime, Vector3.up);
        }
        else if (Input.GetKey(KeyCode.Alpha0))
        {
            _barR.transform.rotation *= Quaternion.AngleAxis(-angularSpeed * Time.deltaTime, Vector3.up);
        }

        var scroll = Input.mouseScrollDelta.y;
        _mainCamera.transform.position += -_mainCamera.transform.forward * scroll * zoomSpeed * Time.deltaTime;


        if (Input.GetKeyDown(KeyCode.R))
        {
            _emitter.KillAll();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            _fluidType++;
            if (_fluidType >= EFluidType.Num)
            {
                _fluidType = EFluidType.Type0;
            }
            setFluidMaterial();

            _emitter.KillAll();
        }
    }

    public enum EFluidType
    {
        Type0,
        Type1,
        Type2,
        Type3,

        Num
    }

    public EFluidType FluidType => _fluidType;

    private void setFluidMaterial()
    {
        Obi.ObiFluidEmitterBlueprint fluidMaterial = _emitter.emitterBlueprint as Obi.ObiFluidEmitterBlueprint;
        switch (_fluidType)
        {
            case EFluidType.Type0:
                fluidMaterial.viscosity = 0f;
                fluidMaterial.smoothing = 2.5f;
                fluidMaterial.surfaceTension = 0.5f;
                fluidMaterial.atmosphericDrag = 0f;

                _barL.GetComponent<Renderer>().material.color = Color.white;
                _barR.GetComponent<Renderer>().material.color = Color.white;

                //_phaseChanger.enabled = false;
                break;

            case EFluidType.Type1:
                fluidMaterial.smoothing = 2f;
                fluidMaterial.viscosity = 2f;
                fluidMaterial.surfaceTension = 0.5f;
                fluidMaterial.atmosphericDrag = 0f;

                _barL.GetComponent<Renderer>().material.color = Color.white;
                _barR.GetComponent<Renderer>().material.color = Color.white;

                //_phaseChanger.enabled = false;
                break;

            case EFluidType.Type2:
                fluidMaterial.viscosity = 5f;
                fluidMaterial.smoothing = 2f;
                fluidMaterial.surfaceTension = 1f;
                fluidMaterial.atmosphericDrag = 20f;

                _barL.GetComponent<Renderer>().material.color = Color.white;
                _barR.GetComponent<Renderer>().material.color = Color.white;

                //_phaseChanger.enabled = false;
                break;

            case EFluidType.Type3:
                fluidMaterial.viscosity = 0f;
                fluidMaterial.smoothing = 2.5f;
                fluidMaterial.surfaceTension = 0.5f;
                fluidMaterial.atmosphericDrag = 0f;

                _barL.GetComponent<Renderer>().material.color = _colorBlue;
                _barR.GetComponent<Renderer>().material.color = _colorRed;

                //_phaseChanger.enabled = true;
                break;
        }

        _text.text = $"Type_{(int)_fluidType}: T      Reset: R      Zoom: Scroll      Exit: Escape";
    }

    private GameObject _barL;
    private GameObject _barR;
    private GameObject _barLUnder;
    private GameObject _barRUnder;
    private Color _colorBlue;
    private Color _colorRed;
    private Camera _mainCamera;
    private Obi.ObiEmitter _emitter;
    private UnityEngine.UI.Text _text;
    private PhaseChanger _phaseChanger;

    private EFluidType _fluidType = EFluidType.Type0;

    [SerializeField] private float zoomSpeed = 30f;
    [SerializeField] private float barSpeed = 3f;
    [SerializeField] private float angularSpeed = 360f;
}

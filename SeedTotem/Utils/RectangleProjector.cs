using Jotunn.Managers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SeedTotem.Utils
{
    internal class RectangleProjector : MonoBehaviour
    {
        public float cubesSpeed = 1f;
        public float m_length = 2f;
        public float m_width = 2f;

        private static GameObject _segment;

        private static GameObject SelectionSegment
        {
            get
            {
                if (!_segment)
                {
                    GameObject workbench = PrefabManager.Instance.GetPrefab("piece_workbench");
                    _segment = Instantiate(workbench.GetComponentInChildren<CircleProjector>().m_prefab);
                    _segment.SetActive(true);
                }

                return _segment;
            }
        }

        private GameObject rootCube;
        private float cubesThickness = 0.15f;
        private float cubesHeight = 0.1f;
        private float cubesLength = 1f;

        private int cubesPerWidth;
        private int cubesPerLength;
        private float updatesPerSecond = 60f;
        private float cubesLength100;
        private float cubesWidth100;
        private float sideLengthHalved;
        private float sideWidthHalved;
        internal bool isRunning = false;

        private Transform parentNorth;
        private Transform parentEast;
        private Transform parentSouth;
        private Transform parentWest;
        private List<Transform> cubesNorth = new List<Transform>();
        private List<Transform> cubesEast = new List<Transform>();
        private List<Transform> cubesSouth = new List<Transform>();
        private List<Transform> cubesWest = new List<Transform>();

        public void Start()
        {
            rootCube = new GameObject("cube");
            GameObject cubeObject = Instantiate(SelectionSegment);
            cubeObject.transform.SetParent(rootCube.transform);
            cubeObject.transform.localScale = new Vector3(1f, 1f, 1f);
            cubeObject.transform.localPosition = new Vector3(0f, 0f, -0.5f);
            rootCube.transform.localScale = new Vector3(cubesThickness, cubesHeight, cubesLength);
            rootCube.SetActive(true);

            RefreshStuff();
            StartProjecting();
        }

        private void OnEnable()
        {
            if (isRunning || parentNorth == null)
            {
                return;
            }
            isRunning = true;

            StartMarchingCubes();
        }

        private void StartMarchingCubes()
        {
            StartCoroutine(AnimateElements(parentNorth, cubesNorth, false));
            StartCoroutine(AnimateElements(parentEast, cubesEast, true));
            StartCoroutine(AnimateElements(parentSouth, cubesSouth, false));
            StartCoroutine(AnimateElements(parentWest, cubesWest, true));
        }

        private void OnDisable()
        {
            isRunning = false;
        }

        public void StartProjecting()
        {
            if (isRunning)
            {
                return;
            }
            isRunning = true;

            parentNorth = CreateElements(0, cubesNorth);
            parentEast = CreateElements(90, cubesEast);
            parentSouth = CreateElements(180, cubesSouth);
            parentWest = CreateElements(270, cubesWest);

            StartMarchingCubes();
        }

        public void StopProjecting()
        {
            if (!isRunning)
            {
                return;
            }
            isRunning = false;

            StopAllCoroutines();

            Destroy(parentNorth.gameObject);
            Destroy(parentEast.gameObject);
            Destroy(parentSouth.gameObject);
            Destroy(parentWest.gameObject);

            cubesNorth.Clear();
            cubesEast.Clear();
            cubesSouth.Clear();
            cubesWest.Clear();
        }

        internal void RefreshStuff(bool force = false)
        {
            cubesPerLength = Mathf.FloorToInt(m_length / 2f);
            cubesPerWidth = Mathf.FloorToInt(m_width / 2f);

            cubesLength100 = m_length / cubesPerLength;
            cubesWidth100 = m_width / cubesPerWidth;
            sideLengthHalved = m_length / 2f;
            sideWidthHalved = m_width / 2f;
            if (isRunning &&
                (force || (cubesPerLength + 1 != cubesNorth.Count || cubesPerWidth + 1 != cubesEast.Count)))
            {
                StopProjecting();
                StartProjecting();
            }
        }

        private Transform CreateElements(int rotation, List<Transform> cubes)
        {
            // Spawn parent object, each which represent a side of the cube
            Transform cubesParent = new GameObject(rotation.ToString()).transform;
            cubesParent.transform.position = transform.position;
            cubesParent.transform.rotation = transform.rotation;
            cubesParent.transform.RotateAround(transform.position, Vector3.up, rotation);
            cubesParent.SetParent(transform);

            // Spawn cubes
            int cubesPerSide = rotation % 180 == 0 ? cubesPerLength : cubesPerWidth;

            for (int i = 0; i < cubesPerSide + 1; i++)
            {
                cubes.Add(Instantiate(rootCube, transform.position, Quaternion.identity, cubesParent).transform);
            }

            // Spawn helper objects
            Transform a = new GameObject("Start").transform;
            Transform b = new GameObject("End").transform;
            a.SetParent(cubesParent);
            b.SetParent(cubesParent);

            // Initial cube values
            for (int i = 0; i < cubes.Count; i++)
            {
                cubes[i].forward = cubesParent.right;
            }

            return cubesParent;
        }

        private IEnumerator AnimateElements(Transform cubeParent, List<Transform> cubes, bool length)
        {
            Transform a = cubeParent.Find("Start");
            Transform b = cubeParent.Find("End");

            int cubesPerSide = length ? cubesPerLength : cubesPerWidth;
            float sideLength = length ? m_length : m_width;
            float halfSideLength = length ? sideLengthHalved : sideWidthHalved;
            float halfSideWidth = length ? sideWidthHalved : sideLengthHalved;
            float cubes100 = length ? cubesLength100 : cubesWidth100;

            // Animation
            while (true)
            {
                RefreshStuff(); // R

                a.position = cubeParent.forward * (halfSideWidth - cubesThickness / 2) - cubeParent.right * halfSideLength + cubeParent.position; // R
                b.position = cubeParent.forward * (halfSideWidth - cubesThickness / 2) + cubeParent.right * halfSideLength + cubeParent.position; // R
                Vector3 dir = b.position - a.position;

                for (int i = 0; i < cubes.Count; i++)
                {
                    Transform cube = cubes[i];
                    cube.gameObject.SetActive(true);

                    // Deterministic, baby
                    float pos = (Time.time * cubesSpeed + (sideLength / cubesPerSide) * i) % (sideLength + cubes100);

                    if (pos < cubesLength)                                              // Is growing
                    {
                        cube.position = dir.normalized * pos + a.position;
                        cube.localScale = new Vector3(cube.localScale.x, cube.localScale.y, (cubesLength - (cubesLength - pos)));
                    }
                    else if (pos >= sideLength && pos <= sideLength + cubesLength)      // Is shrinking
                    {
                        cube.position = dir.normalized * sideLength + a.position;
                        cube.localScale = new Vector3(cube.localScale.x, cube.localScale.y, (cubesLength - (pos - sideLength)));
                    }
                    else if (pos >= sideLength && pos >= sideLength + cubesLength)      // Is waiting
                    {
                        cube.gameObject.SetActive(false);
                    }
                    else                                                                // Need to move
                    {
                        cube.position = dir.normalized * pos + a.position;
                        cube.localScale = new Vector3(cube.localScale.x, cube.localScale.y, cubesLength);
                    }
                }
                yield return new WaitForSecondsRealtime(1 / updatesPerSecond);
            }
        }
    }
}
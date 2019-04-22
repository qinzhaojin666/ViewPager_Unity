﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace IL_TabView {
    [RequireComponent (typeof (Image))]
    [RequireComponent (typeof (Mask))]
    [RequireComponent (typeof (ScrollRect))]
    public abstract class TabViewScroll : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler {
        [Tooltip ("Set starting page index - starting from 0")]
        public int startingPage = 0;
        [Tooltip ("Threshold time for fast swipe in seconds")]
        public float fastSwipeThresholdTime = 0.3f;
        [Tooltip ("Threshold time for fast swipe in (unscaled) pixels")]
        public int fastSwipeThresholdDistance = 100;
        [Tooltip ("How fast will page lerp to target position")]
        public float decelerationRate = 10f;
        [Tooltip ("Button to go to the previous page (optional)")]
        public GameObject prevButton;
        [Tooltip ("Button to go to the next page (optional)")]
        public GameObject nextButton;
        [Tooltip ("Sprite for unselected page (optional)")]
        public Sprite unselectedPage;
        [Tooltip ("Sprite for selected page (optional)")]
        public Sprite selectedPage;
        [Tooltip ("Container with page images (optional)")]
        public Transform pageSelectionIcons;
        [Tooltip ("List Of Buttons To Open the pages in order of their index")]
        public List<Button> tabButtons;

        private int fastSwipeMaxLimit;
        private ScrollRect scrollRectComponent;
        private RectTransform scrollRectRect;
        private RectTransform container;
        // number of pages in container
        private int pageCount;
        private int currentPage;

        // whether lerping is in progress and target lerp position
        private bool lerp;
        private Vector2 lerpTo;

        // target position of every page
        private List<Vector2> pagePositions = new List<Vector2> ();

        // in draggging, when dragging started and where it started
        private bool dragging;
        private float timeStamp;
        private Vector2 startPosition;

        // for showing small page icons
        private bool showPageSelection;
        private int previousPageSelectionIndex;
        // container with Image components - one Image for each page
        private List<Image> pageSelectionImages;

        public abstract void IL_Start();
        public abstract void IL_Update();
        //------------------------------------------------------------------------
        void Start () {
            scrollRectComponent = GetComponent<ScrollRect> ();
            scrollRectRect = GetComponent<RectTransform> ();
            container = scrollRectComponent.content;
            pageCount = container.childCount;
            lerp = false;
            if (tabButtons.Count > 0) {
                for (int i = 0; i < pageCount; i++) {
                    int x = i;
                    tabButtons[i].onClick.AddListener (() => SetPage (x));
                }
            }
            // init
            SetPagePositions ();
            SetPage (startingPage);
            InitPageSelection ();
            SetPageSelection (startingPage);
            IL_Start ();
        }

        public void OnDisable () {
            for (int i = 0; i < pageCount; i++) {
                int x = i;
                tabButtons[i].onClick.RemoveListener (() => SetPage (x));
            }
        }
        //------------------------------------------------------------------------
        void Update () {
            // if moving to target position
            if (lerp) {
                // prevent overshooting with values greater than 1
                float decelerate = Mathf.Min (decelerationRate * Time.deltaTime, 1f);
                container.anchoredPosition = Vector2.Lerp (container.anchoredPosition, lerpTo, decelerate);
                // time to stop lerping?
                if (Vector2.SqrMagnitude (container.anchoredPosition - lerpTo) < 0.25f) {
                    // snap to target and stop lerping
                    container.anchoredPosition = lerpTo;
                    lerp = false;
                    // clear also any scrollrect move that may interfere with our lerping
                    scrollRectComponent.velocity = Vector2.zero;
                }
                // switches selection icon exactly to correct page
                if (showPageSelection) {
                    SetPageSelection (GetNearestPage ());
                }
            }
           IL_Update ();
        }

        //------------------------------------------------------------------------
        private void SetPagePositions () {
            int width = 0;
            int offsetX = 0;
            int containerWidth = 0;
            int containerHeight = 0;

            // screen width in pixels of scrollrect window
            width = (int) scrollRectRect.rect.width;
            // center position of all pages
            offsetX = width / 2;
            // total width
            containerWidth = width * pageCount;
            // limit fast swipe length - beyond this length it is fast swipe no more
            fastSwipeMaxLimit = width;

            // set width of container
            Vector2 newSize = new Vector2 (containerWidth, containerHeight);
            container.sizeDelta = newSize;
            Vector2 newPosition = new Vector2 (containerWidth / 2, containerHeight / 2);
            container.anchoredPosition = newPosition;

            // delete any previous settings
            pagePositions.Clear ();

            // iterate through all container childern and set their positions
            for (int i = 0; i < pageCount; i++) {
                RectTransform child = container.GetChild (i).GetComponent<RectTransform> ();
                Vector2 childPosition;
                childPosition = new Vector2 (i * width - containerWidth / 2 + offsetX, 0f);
                child.anchoredPosition = childPosition;
                pagePositions.Add (-childPosition);
            }

        }

        //------------------------------------------------------------------------
        private void SetPage (int aPageIndex) {
            Debug.Log (aPageIndex);
            aPageIndex = Mathf.Clamp (aPageIndex, 0, pageCount - 1);
            container.anchoredPosition = pagePositions[aPageIndex];
            currentPage = aPageIndex;
        }

        //------------------------------------------------------------------------
        public void LerpToPage (int aPageIndex) {
            aPageIndex = Mathf.Clamp (aPageIndex, 0, pageCount - 1);
            lerpTo = pagePositions[aPageIndex];
            lerp = true;
            currentPage = aPageIndex;
        }

        //------------------------------------------------------------------------
        private void InitPageSelection () {
            // page selection - only if defined sprites for selection icons
            showPageSelection = unselectedPage != null && selectedPage != null;
            if (showPageSelection) {
                // also container with selection images must be defined and must have exatly the same amount of items as pages container
                if (pageSelectionIcons == null || pageSelectionIcons.childCount != pageCount) {
                    Debug.LogWarning ("Different count of pages and selection icons - will not show page selection");
                    showPageSelection = false;
                } else {
                    previousPageSelectionIndex = -1;
                    pageSelectionImages = new List<Image> ();

                    // cache all Image components into list
                    for (int i = 0; i < pageSelectionIcons.childCount; i++) {
                        Image image = pageSelectionIcons.GetChild (i).GetComponent<Image> ();
                        if (image == null) {
                            Debug.LogWarning ("Page selection icon at position " + i + " is missing Image component");
                        }
                        pageSelectionImages.Add (image);
                    }
                }
            }
        }

        //------------------------------------------------------------------------
        private void SetPageSelection (int aPageIndex) {
            // nothing to change
            if (previousPageSelectionIndex == aPageIndex) {
                return;
            }

            // unselect old
            if (previousPageSelectionIndex >= 0) {
                pageSelectionImages[previousPageSelectionIndex].sprite = unselectedPage;
                pageSelectionImages[previousPageSelectionIndex].SetNativeSize ();
            }

            // select new
            pageSelectionImages[aPageIndex].sprite = selectedPage;
            pageSelectionImages[aPageIndex].SetNativeSize ();

            previousPageSelectionIndex = aPageIndex;
        }

        //------------------------------------------------------------------------
        private void NextScreen () {
            LerpToPage (currentPage + 1);
        }

        //------------------------------------------------------------------------
        private void PreviousScreen () {
            LerpToPage (currentPage - 1);
        }

        //------------------------------------------------------------------------
        private int GetNearestPage () {
            // based on distance from current position, find nearest page
            Vector2 currentPosition = container.anchoredPosition;

            float distance = float.MaxValue;
            int nearestPage = currentPage;

            for (int i = 0; i < pagePositions.Count; i++) {
                float testDist = Vector2.SqrMagnitude (currentPosition - pagePositions[i]);
                if (testDist < distance) {
                    distance = testDist;
                    nearestPage = i;
                }
            }

            return nearestPage;
        }

        //------------------------------------------------------------------------
        public void OnBeginDrag (PointerEventData aEventData) {
            // if currently lerping, then stop it as user is draging
            lerp = false;
            // not dragging yet
            dragging = false;
        }

        //------------------------------------------------------------------------
        public void OnEndDrag (PointerEventData aEventData) {
            // how much was container's content dragged
            float difference;

            difference = startPosition.x - container.anchoredPosition.x;

            // test for fast swipe - swipe that moves only +/-1 item
            if (Time.unscaledTime - timeStamp < fastSwipeThresholdTime &&
                Mathf.Abs (difference) > fastSwipeThresholdDistance &&
                Mathf.Abs (difference) < fastSwipeMaxLimit) {
                if (difference > 0) {
                    NextScreen ();
                } else {
                    PreviousScreen ();
                }
            } else {
                // if not fast time, look to which page we got to
                LerpToPage (GetNearestPage ());
            }

            dragging = false;
        }

        //------------------------------------------------------------------------
        public void OnDrag (PointerEventData aEventData) {
            if (!dragging) {
                // dragging started
                dragging = true;
                // save time - unscaled so pausing with Time.scale should not affect it
                timeStamp = Time.unscaledTime;
                // save current position of cointainer
                startPosition = container.anchoredPosition;
            } else {
                if (showPageSelection) {
                    SetPageSelection (GetNearestPage ());
                }
            }
        }
    }
}
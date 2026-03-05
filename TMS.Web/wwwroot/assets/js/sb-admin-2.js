(function($) {
  "use strict"; // Start of use strict

  // ========================================
  // ✅ SIDEBAR STATE PERSISTENCE
  // Load saved state on page load (before any animation)
  // ========================================
  var savedSidebarState = localStorage.getItem('sidebarToggled');
  if (savedSidebarState === 'true') {
    $("body").addClass("sidebar-toggled");
    $(".sidebar").addClass("toggled");
  }

  // Toggle the side navigation
  $("#sidebarToggle, #sidebarToggleTop").on('click', function(e) {
    $("body").toggleClass("sidebar-toggled");
    $(".sidebar").toggleClass("toggled");
    
    // ✅ Save sidebar state to localStorage
    var isToggled = $(".sidebar").hasClass("toggled");
    localStorage.setItem('sidebarToggled', isToggled);
    
    if (isToggled) {
      $('.sidebar .collapse').collapse('hide');
    };
  });

  // Close any open menu accordions when window is resized below 768px
  $(window).resize(function() {
    if ($(window).width() < 768) {
      $('.sidebar .collapse').collapse('hide');
    };
    
    // Toggle the side navigation when window is resized below 480px
    if ($(window).width() < 480 && !$(".sidebar").hasClass("toggled")) {
      $("body").addClass("sidebar-toggled");
      $(".sidebar").addClass("toggled");
      $('.sidebar .collapse').collapse('hide');
      // ✅ Save state
      localStorage.setItem('sidebarToggled', 'true');
    };
  });

  // Prevent the content wrapper from scrolling when the fixed side navigation hovered over
  $('body.fixed-nav .sidebar').on('mousewheel DOMMouseScroll wheel', function(e) {
    if ($(window).width() > 768) {
      var e0 = e.originalEvent,
        delta = e0.wheelDelta || -e0.detail;
      this.scrollTop += (delta < 0 ? 1 : -1) * 30;
      e.preventDefault();
    }
  });

  // Scroll to top button appear
  $(document).on('scroll', function() {
    var scrollDistance = $(this).scrollTop();
    if (scrollDistance > 100) {
      $('.scroll-to-top').fadeIn();
    } else {
      $('.scroll-to-top').fadeOut();
    }
  });

  // Smooth scrolling using jQuery easing
  $(document).on('click', 'a.scroll-to-top', function(e) {
    var $anchor = $(this);
    $('html, body').stop().animate({
      scrollTop: ($($anchor.attr('href')).offset().top)
    }, 1000, 'easeInOutExpo');
    e.preventDefault();
  });

})(jQuery); // End of use strict
/**
   * --------------------------------------------------------------------------
   * Bootstrap (v5.1.3): modal.js
   * Licensed under MIT (https://github.com/twbs/bootstrap/blob/main/LICENSE)
   * --------------------------------------------------------------------------
   */
/**
 * ------------------------------------------------------------------------
 * Constants
 * ------------------------------------------------------------------------
 */

const NAME$6 = 'modal';
const DATA_KEY$6 = 'bs.modal';
const EVENT_KEY$6 = `.${DATA_KEY$6}`;
const DATA_API_KEY$3 = '.data-api';
const ESCAPE_KEY$1 = 'Escape';
const Default$5 = {
    backdrop: true,
    keyboard: true,
    focus: true
};
const DefaultType$5 = {
    backdrop: '(boolean|string)',
    keyboard: 'boolean',
    focus: 'boolean'
};
const EVENT_HIDE$3 = `hide${EVENT_KEY$6}`;
const EVENT_HIDE_PREVENTED = `hidePrevented${EVENT_KEY$6}`;
const EVENT_HIDDEN$3 = `hidden${EVENT_KEY$6}`;
const EVENT_SHOW$3 = `show${EVENT_KEY$6}`;
const EVENT_SHOWN$3 = `shown${EVENT_KEY$6}`;
const EVENT_RESIZE = `resize${EVENT_KEY$6}`;
const EVENT_CLICK_DISMISS = `click.dismiss${EVENT_KEY$6}`;
const EVENT_KEYDOWN_DISMISS$1 = `keydown.dismiss${EVENT_KEY$6}`;
const EVENT_MOUSEUP_DISMISS = `mouseup.dismiss${EVENT_KEY$6}`;
const EVENT_MOUSEDOWN_DISMISS = `mousedown.dismiss${EVENT_KEY$6}`;
const EVENT_CLICK_DATA_API$2 = `click${EVENT_KEY$6}${DATA_API_KEY$3}`;
const CLASS_NAME_OPEN = 'modal-open';
const CLASS_NAME_FADE$3 = 'fade';
const CLASS_NAME_SHOW$4 = 'show';
const CLASS_NAME_STATIC = 'modal-static';
const OPEN_SELECTOR$1 = '.modal.show';
const SELECTOR_DIALOG = '.modal-dialog';
const SELECTOR_MODAL_BODY = '.modal-body';
const SELECTOR_DATA_TOGGLE$2 = '[data-bs-toggle="modal"]';
/**
 * ------------------------------------------------------------------------
 * Class Definition
 * ------------------------------------------------------------------------
 */

class Modal extends BaseComponent {
    constructor(element, config) {
        super(element);
        this._config = this._getConfig(config);
        this._dialog = SelectorEngine.findOne(SELECTOR_DIALOG, this._element);
        this._backdrop = this._initializeBackDrop();
        this._focustrap = this._initializeFocusTrap();
        this._isShown = false;
        this._ignoreBackdropClick = false;
        this._isTransitioning = false;
        this._scrollBar = new ScrollBarHelper();
    } // Getters


    static get Default() {
        return Default$5;
    }

    static get NAME() {
        return NAME$6;
    } // Public


    toggle(relatedTarget) {
        return this._isShown ? this.hide() : this.show(relatedTarget);
    }

    show(relatedTarget) {
        if (this._isShown || this._isTransitioning) {
            return;
        }

        const showEvent = EventHandler.trigger(this._element, EVENT_SHOW$3, {
            relatedTarget
        });

        if (showEvent.defaultPrevented) {
            return;
        }

        this._isShown = true;

        if (this._isAnimated()) {
            this._isTransitioning = true;
        }

        this._scrollBar.hide();

        document.body.classList.add(CLASS_NAME_OPEN);

        this._adjustDialog();

        this._setEscapeEvent();

        this._setResizeEvent();

        EventHandler.on(this._dialog, EVENT_MOUSEDOWN_DISMISS, () => {
            EventHandler.one(this._element, EVENT_MOUSEUP_DISMISS, event => {
                if (event.target === this._element) {
                    this._ignoreBackdropClick = true;
                }
            });
        });

        this._showBackdrop(() => this._showElement(relatedTarget));
    }

    hide() {
        if (!this._isShown || this._isTransitioning) {
            return;
        }

        const hideEvent = EventHandler.trigger(this._element, EVENT_HIDE$3);

        if (hideEvent.defaultPrevented) {
            return;
        }

        this._isShown = false;

        const isAnimated = this._isAnimated();

        if (isAnimated) {
            this._isTransitioning = true;
        }

        this._setEscapeEvent();

        this._setResizeEvent();

        this._focustrap.deactivate();

        this._element.classList.remove(CLASS_NAME_SHOW$4);

        EventHandler.off(this._element, EVENT_CLICK_DISMISS);
        EventHandler.off(this._dialog, EVENT_MOUSEDOWN_DISMISS);

        this._queueCallback(() => this._hideModal(), this._element, isAnimated);
    }

    dispose() {
        [window, this._dialog].forEach(htmlElement => EventHandler.off(htmlElement, EVENT_KEY$6));

        this._backdrop.dispose();

        this._focustrap.deactivate();

        super.dispose();
    }

    handleUpdate() {
        this._adjustDialog();
    } // Private


    _initializeBackDrop() {
        return new Backdrop({
            isVisible: Boolean(this._config.backdrop),
            // 'static' option will be translated to true, and booleans will keep their value
            isAnimated: this._isAnimated()
        });
    }

    _initializeFocusTrap() {
        return new FocusTrap({
            trapElement: this._element
        });
    }

    _getConfig(config) {
        config = {
            ...Default$5,
            ...Manipulator.getDataAttributes(this._element),
            ...(typeof config === 'object' ? config : {})
        };
        typeCheckConfig(NAME$6, config, DefaultType$5);
        return config;
    }

    _showElement(relatedTarget) {
        const isAnimated = this._isAnimated();

        const modalBody = SelectorEngine.findOne(SELECTOR_MODAL_BODY, this._dialog);

        if (!this._element.parentNode || this._element.parentNode.nodeType !== Node.ELEMENT_NODE) {
            // Don't move modal's DOM position
            document.body.append(this._element);
        }

        this._element.style.display = 'block';

        this._element.removeAttribute('aria-hidden');

        this._element.setAttribute('aria-modal', true);

        this._element.setAttribute('role', 'dialog');

        this._element.scrollTop = 0;

        if (modalBody) {
            modalBody.scrollTop = 0;
        }

        if (isAnimated) {
            reflow(this._element);
        }

        this._element.classList.add(CLASS_NAME_SHOW$4);

        const transitionComplete = () => {
            if (this._config.focus) {
                this._focustrap.activate();
            }

            this._isTransitioning = false;
            EventHandler.trigger(this._element, EVENT_SHOWN$3, {
                relatedTarget
            });
        };

        this._queueCallback(transitionComplete, this._dialog, isAnimated);
    }

    _setEscapeEvent() {
        if (this._isShown) {
            EventHandler.on(this._element, EVENT_KEYDOWN_DISMISS$1, event => {
                if (this._config.keyboard && event.key === ESCAPE_KEY$1) {
                    event.preventDefault();
                    this.hide();
                } else if (!this._config.keyboard && event.key === ESCAPE_KEY$1) {
                    this._triggerBackdropTransition();
                }
            });
        } else {
            EventHandler.off(this._element, EVENT_KEYDOWN_DISMISS$1);
        }
    }

    _setResizeEvent() {
        if (this._isShown) {
            EventHandler.on(window, EVENT_RESIZE, () => this._adjustDialog());
        } else {
            EventHandler.off(window, EVENT_RESIZE);
        }
    }

    _hideModal() {
        this._element.style.display = 'none';

        this._element.setAttribute('aria-hidden', true);

        this._element.removeAttribute('aria-modal');

        this._element.removeAttribute('role');

        this._isTransitioning = false;

        this._backdrop.hide(() => {
            document.body.classList.remove(CLASS_NAME_OPEN);

            this._resetAdjustments();

            this._scrollBar.reset();

            EventHandler.trigger(this._element, EVENT_HIDDEN$3);
        });
    }

    _showBackdrop(callback) {
        EventHandler.on(this._element, EVENT_CLICK_DISMISS, event => {
            if (this._ignoreBackdropClick) {
                this._ignoreBackdropClick = false;
                return;
            }

            if (event.target !== event.currentTarget) {
                return;
            }

            if (this._config.backdrop === true) {
                this.hide();
            } else if (this._config.backdrop === 'static') {
                this._triggerBackdropTransition();
            }
        });

        this._backdrop.show(callback);
    }

    _isAnimated() {
        return this._element.classList.contains(CLASS_NAME_FADE$3);
    }

    _triggerBackdropTransition() {
        const hideEvent = EventHandler.trigger(this._element, EVENT_HIDE_PREVENTED);

        if (hideEvent.defaultPrevented) {
            return;
        }

        const {
            classList,
            scrollHeight,
            style
        } = this._element;
        const isModalOverflowing = scrollHeight > document.documentElement.clientHeight; // return if the following background transition hasn't yet completed

        if (!isModalOverflowing && style.overflowY === 'hidden' || classList.contains(CLASS_NAME_STATIC)) {
            return;
        }

        if (!isModalOverflowing) {
            style.overflowY = 'hidden';
        }

        classList.add(CLASS_NAME_STATIC);

        this._queueCallback(() => {
            classList.remove(CLASS_NAME_STATIC);

            if (!isModalOverflowing) {
                this._queueCallback(() => {
                    style.overflowY = '';
                }, this._dialog);
            }
        }, this._dialog);

        this._element.focus();
    } // ----------------------------------------------------------------------
    // the following methods are used to handle overflowing modals
    // ----------------------------------------------------------------------


    _adjustDialog() {
        const isModalOverflowing = this._element.scrollHeight > document.documentElement.clientHeight;

        const scrollbarWidth = this._scrollBar.getWidth();

        const isBodyOverflowing = scrollbarWidth > 0;

        if (!isBodyOverflowing && isModalOverflowing && !isRTL() || isBodyOverflowing && !isModalOverflowing && isRTL()) {
            this._element.style.paddingLeft = `${scrollbarWidth}px`;
        }

        if (isBodyOverflowing && !isModalOverflowing && !isRTL() || !isBodyOverflowing && isModalOverflowing && isRTL()) {
            this._element.style.paddingRight = `${scrollbarWidth}px`;
        }
    }

    _resetAdjustments() {
        this._element.style.paddingLeft = '';
        this._element.style.paddingRight = '';
    } // Static


    static jQueryInterface(config, relatedTarget) {
        return this.each(function () {
            const data = Modal.getOrCreateInstance(this, config);

            if (typeof config !== 'string') {
                return;
            }

            if (typeof data[config] === 'undefined') {
                throw new TypeError(`No method named "${config}"`);
            }

            data[config](relatedTarget);
        });
    }

}
/**
 * ------------------------------------------------------------------------
 * Data Api implementation
 * ------------------------------------------------------------------------
 */


EventHandler.on(document, EVENT_CLICK_DATA_API$2, SELECTOR_DATA_TOGGLE$2, function (event) {
    const target = getElementFromSelector(this);

    if (['A', 'AREA'].includes(this.tagName)) {
        event.preventDefault();
    }

    EventHandler.one(target, EVENT_SHOW$3, showEvent => {
        if (showEvent.defaultPrevented) {
            // only register focus restorer if modal will actually get shown
            return;
        }

        EventHandler.one(target, EVENT_HIDDEN$3, () => {
            if (isVisible(this)) {
                this.focus();
            }
        });
    }); // avoid conflict when clicking moddal toggler while another one is open

    const allReadyOpen = SelectorEngine.findOne(OPEN_SELECTOR$1);

    if (allReadyOpen) {
        Modal.getInstance(allReadyOpen).hide();
    }

    const data = Modal.getOrCreateInstance(target);
    data.toggle(this);
});
enableDismissTrigger(Modal);
/**
  * ------------------------------------------------------------------------
  * jQuery
  * ------------------------------------------------------------------------
  * add .Modal to jQuery only if jQuery is present
  */

defineJQueryPlugin(Modal);
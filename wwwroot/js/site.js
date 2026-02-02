// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

/* site.js
   - Moved dropdown hover/open behavior from _Layout.cshtml here.
   - Keeps behavior:
     * hover opens dropdown on desktop (min-width: 768px)
     * preserves normal click/touch behavior on mobile
   - Includes defensive unbind to avoid duplicate handlers on resize.
*/
(function ($) {
    "use strict";

    // Ensure jQuery is available
    if (typeof $ === "undefined") {
        console.warn("jQuery not found — site.js needs jQuery for dropdown behaviour.");
        return;
    }

    $(function () {
        // Enable hover-to-open for desktop widths, remove handlers on mobile.
        function enableHoverDropdown() {
            var $profile = $('#profile-li');

            // Always unbind first to avoid duplicate handlers
            $profile.off('mouseenter.profileHover mouseleave.profileHover');

            if (window.matchMedia("(min-width: 768px)").matches) {
                // Bind namespaced events so we can remove them cleanly later
                $profile.on('mouseenter.profileHover', function () {
                    // Add Bootstrap 'show' classes so markup + aria reflect open state
                    $(this).addClass('show');
                    $(this).find('.dropdown-menu').addClass('show');
                    $(this).find('.dropdown-toggle').attr('aria-expanded', 'true');
                });

                $profile.on('mouseleave.profileHover', function () {
                    $(this).removeClass('show');
                    $(this).find('.dropdown-menu').removeClass('show');
                    $(this).find('.dropdown-toggle').attr('aria-expanded', 'false');
                });
            } else {
                // On mobile/touch, ensure Bootstrap's default click behavior is used.
                $profile.removeClass('show');
                $profile.find('.dropdown-menu').removeClass('show');
                $profile.find('.dropdown-toggle').attr('aria-expanded', 'false');
            }
        }

        // Initial enable + re-evaluate on resize (debounced)
        enableHoverDropdown();

        var resizeTimer = null;
        $(window).on('resize', function () {
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(function () {
                enableHoverDropdown();
            }, 150);
        });
    });
}(jQuery));

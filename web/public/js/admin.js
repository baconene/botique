(() => {
    const toggle = document.querySelector('.mobile-nav-toggle');
    const nav = document.querySelector('#admin-nav');
    if (toggle && nav) {
        toggle.hidden = false;
        const mobile = window.matchMedia('(max-width: 800px)');
        const update = () => {
            nav.hidden = mobile.matches;
            toggle.setAttribute('aria-expanded', String(!nav.hidden));
        };
        update();
        mobile.addEventListener('change', update);
        toggle.addEventListener('click', () => {
            nav.hidden = !nav.hidden;
            toggle.setAttribute('aria-expanded', String(!nav.hidden));
        });
        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && mobile.matches && !nav.hidden) {
                nav.hidden = true;
                toggle.setAttribute('aria-expanded', 'false');
                toggle.focus();
            }
        });
    }
    document.querySelectorAll('.print-report').forEach(button => button.addEventListener('click', () => window.print()));
})();

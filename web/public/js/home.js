// Home page motion (GSAP + ScrollTrigger). Content is fully visible without JavaScript,
// without GSAP (CDN blocked) and for visitors who prefer reduced motion.
(() => {
    'use strict';

    const root = document.documentElement;
    const home = document.querySelector('[data-home]');
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (!home || !window.gsap || reduce) {
        root.classList.remove('home-anim');
        return;
    }

    const { gsap } = window;
    if (window.ScrollTrigger) gsap.registerPlugin(window.ScrollTrigger);
    const hasST = Boolean(window.ScrollTrigger);
    const q = (sel, scope = home) => [...scope.querySelectorAll(sel)];

    // ---------- hero intro ----------
    gsap.set(q('[data-hero-line], [data-hero-in], [data-hero-main], [data-hero-cut], [data-orb]'), { visibility: 'visible' });
    root.classList.remove('home-anim');

    const intro = gsap.timeline({ defaults: { ease: 'power4.out' } });
    intro
        .from(q('[data-hero-line]'), { yPercent: 110, duration: 1.15, stagger: 0.12 })
        .from(q('[data-hero-main] img'), { clipPath: 'polygon(0 100%, 100% 100%, 100% 100%, 0 100%)', scale: 1.18, duration: 1.4, ease: 'expo.out' }, 0.15)
        .from(q('[data-hero-cut]'), { x: 80, autoAlpha: 0, duration: 1.1 }, 0.55)
        .from(q('[data-orb]'), { scale: 0, rotate: -120, duration: 0.9, ease: 'back.out(2.2)' }, 0.8)
        .from(q('[data-hero-in]'), { y: 30, autoAlpha: 0, duration: 0.9, stagger: 0.1 }, 0.6);

    // Count the "pieces ready" number up.
    q('[data-count]').forEach((el) => {
        const target = parseInt(el.dataset.count, 10) || 0;
        const counter = { v: 0 };
        intro.to(counter, { v: target, duration: 1.6, ease: 'power2.out', onUpdate: () => { el.textContent = Math.round(counter.v); } }, 0.7);
    });

    // The accent orb keeps floating gently.
    gsap.to(q('[data-orb]'), { y: -14, rotate: 18, duration: 2.6, ease: 'sine.inOut', yoyo: true, repeat: -1, delay: 1.7 });

    if (!hasST) return;
    const { ScrollTrigger } = window;

    // ---------- hero parallax ----------
    gsap.to(q('[data-hero-main] img'), { yPercent: 8, ease: 'none', scrollTrigger: { trigger: '.drq-hero', start: 'top top', end: 'bottom top', scrub: true } });
    gsap.to(q('[data-hero-cut]'), { yPercent: -18, ease: 'none', scrollTrigger: { trigger: '.drq-hero', start: 'top top', end: 'bottom top', scrub: true } });
    gsap.to('.drq-title', { xPercent: -4, ease: 'none', scrollTrigger: { trigger: '.drq-hero', start: 'top top', end: 'bottom top', scrub: true } });

    // ---------- generic reveals ----------
    ScrollTrigger.batch(q('[data-reveal]'), {
        start: 'top 86%',
        once: true,
        onEnter: (els) => gsap.from(els, { y: 50, autoAlpha: 0, duration: 1, ease: 'power3.out', stagger: 0.12, overwrite: true }),
    });

    // Photos open like a curtain, then drift slightly while scrolling.
    q('[data-reveal-photo]').forEach((fig) => {
        const img = fig.querySelector('img');
        gsap.from(img, { scale: 1.25, duration: 1.6, ease: 'expo.out', scrollTrigger: { trigger: fig, start: 'top 85%', once: true } });
        gsap.from(fig, { clipPath: 'polygon(0 0, 0 0, 0 100%, 0 100%)', duration: 1.3, ease: 'power4.inOut', scrollTrigger: { trigger: fig, start: 'top 85%', once: true },
            onComplete: () => gsap.set(fig, { clearProps: 'clipPath' }) });
        gsap.to(img, { yPercent: -6, ease: 'none', scrollTrigger: { trigger: fig, start: 'top bottom', end: 'bottom top', scrub: true } });
    });

    // List rows (categories, new arrivals) slide in one after another.
    ScrollTrigger.batch(q('[data-list-item]'), {
        start: 'top 92%',
        once: true,
        onEnter: (els) => gsap.from(els, { x: -40, autoAlpha: 0, duration: 0.8, ease: 'power3.out', stagger: 0.07 }),
    });

    // ---------- dark mood frames ----------
    const frames = q('[data-mood-frame]');
    if (frames.length) {
        gsap.from(frames, {
            y: 120, autoAlpha: 0, rotate: (i) => [-4, 0, 4][i % 3], duration: 1.3, ease: 'power4.out', stagger: 0.15,
            scrollTrigger: { trigger: '.drq-mood-frames', start: 'top 80%', once: true },
        });
        frames.forEach((frame, i) => {
            gsap.to(frame, { y: [-40, -90, -20][i % 3], ease: 'none', scrollTrigger: { trigger: '.drq-mood', start: 'top bottom', end: 'bottom top', scrub: true } });
        });
    }

    // ---------- featured cards ----------
    ScrollTrigger.batch(q('[data-card]'), {
        start: 'top 90%',
        once: true,
        onEnter: (els) => gsap.from(els, { y: 60, autoAlpha: 0, duration: 0.9, ease: 'power3.out', stagger: 0.08 }),
    });

    // Re-animate the cards that stay visible when a filter is chosen (filtering itself lives in storefront.js).
    const filters = home.querySelector('[data-collection-filters]');
    if (filters) {
        filters.addEventListener('click', (e) => {
            if (!e.target.closest('button')) return;
            requestAnimationFrame(() => {
                const shown = q('[data-card]').filter((c) => !c.hidden);
                gsap.fromTo(shown, { y: 24, autoAlpha: 0, scale: 0.97 }, { y: 0, autoAlpha: 1, scale: 1, duration: 0.55, ease: 'power3.out', stagger: 0.05, overwrite: true });
                ScrollTrigger.refresh();
            });
        });
    }

    // Images that load late change the layout: keep trigger positions accurate.
    window.addEventListener('load', () => ScrollTrigger.refresh());
})();

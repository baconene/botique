(() => {
    'use strict';

    // Native disclosures remain functional when JavaScript is unavailable.
    const headerDisclosures = [...document.querySelectorAll('.boutique-header details')];
    headerDisclosures.forEach((disclosure) => {
        disclosure.addEventListener('toggle', () => {
            if (disclosure.open) {
                headerDisclosures.forEach((other) => { if (other !== disclosure) other.open = false; });
            }
        });
        disclosure.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && disclosure.open) {
                disclosure.open = false;
                disclosure.querySelector('summary').focus();
            }
        });
        disclosure.querySelectorAll('nav a').forEach((link) => {
            link.addEventListener('click', () => { disclosure.open = false; });
        });
    });
    document.addEventListener('click', (event) => {
        headerDisclosures.forEach((disclosure) => {
            if (disclosure.open && !disclosure.contains(event.target)) disclosure.open = false;
        });
    });

    const studio = document.querySelector('[data-style-studio]');
    if (studio) {
        const choices = [...studio.querySelectorAll('[data-style-choice]')];
        const panels = [...studio.querySelectorAll('[data-style-panel]')];
        const chooseStyle = (selected) => {
            choices.forEach((choice) => choice.setAttribute('aria-pressed', String(choice.dataset.styleChoice === selected)));
            panels.forEach((panel) => { panel.hidden = panel.dataset.stylePanel !== selected; });
        };
        chooseStyle('everyday');
        studio.querySelector('.style-choices').hidden = false;
        choices.forEach((choice) => choice.addEventListener('click', () => chooseStyle(choice.dataset.styleChoice)));
    }

    const filters = document.querySelector('[data-collection-filters]');
    if (filters) {
        const cards = [...document.querySelectorAll('[data-featured-category]')];
        const count = document.querySelector('[data-collection-count]');
        filters.hidden = false;
        filters.querySelectorAll('button').forEach((button) => {
            button.addEventListener('click', () => {
                const category = button.dataset.category;
                let shown = 0;
                cards.forEach((card) => {
                    card.hidden = Boolean(category) && card.dataset.featuredCategory !== category;
                    if (!card.hidden) shown++;
                });
                filters.querySelectorAll('button').forEach((choice) => choice.setAttribute('aria-pressed', String(choice === button)));
                count.textContent = `${shown} featured ${shown === 1 ? 'style' : 'styles'}${category ? ` in ${category}` : ''}`;
            });
        });
    }
})();

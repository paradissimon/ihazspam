/// <binding />
var gulp = require('gulp');
var concat = require('gulp-concat');
var declare = require('gulp-declare');
var handlebars = require('gulp-handlebars');
var uglify = require('gulp-uglify');
var concatcss = require('gulp-concat-css');
var cleancss = require('gulp-clean-css');
var wrap = require('gulp-wrap');
var empty = require('gulp-empty');
var plumber = require('gulp-plumber');

gulp.task('pack', function () {
    var minimize = true;
    var dotjs = minimize ? '.min.js' : '.js';
    var dotcss = minimize ? '.min.css' : '.css';
        

    console.log('>>> Compiling Handlebars templates...');
    gulp.src('src/templates/*.hbs')
        .pipe(plumber())
        .pipe(handlebars({
            // force usage of Handlebars version as declared in devDependencies of package.json
            handlebars: require('handlebars')
        }))
        .pipe(wrap('Handlebars.template(<%= contents %>)'))
        .pipe(declare({
            namespace: 'Templates',
            noRedeclare: true
        }))
        .pipe(concat('handlebars-precompiled-templates.js'))
        .pipe(minimize ? uglify() : empty())
        .pipe(gulp.dest('src/templates/'));

    
    console.log('>>> Concatenating JS...');
    gulp.src([
        // 'node_modules/jquery/dist/jquery' + dotjs,
        'node_modules/handlebars/dist/handlebars.runtime' + dotjs,
        'node_modules/clipboard/dist/clipboard' + dotjs,
        'node_modules/iframe-resizer/js/iframeResizer' + dotjs,
        'src/templates/handlebars-precompiled-templates.js'
        ])
        .pipe(plumber())
        .pipe(concat('ihazspam.js'))
        .pipe(gulp.dest('wwwroot/pack/'));

    
    console.log('>>> Concatenating CSS...');
    gulp.src([
        'src/semantic-ui-2.2.7/components/reset' + dotcss,
        'src/semantic-ui-2.2.7/components/site' + dotcss,
        'src/semantic-ui-2.2.7/components/menu' + dotcss,
        'src/semantic-ui-2.2.7/components/dropdown' + dotcss,
        // 'src/semantic-ui-2.2.7/components/icon' + dotcss,
        'src/semantic-ui-2.2.7/components/container' + dotcss,
        'src/semantic-ui-2.2.7/components/message' + dotcss,
        'src/semantic-ui-2.2.7/components/button' + dotcss,
        'src/semantic-ui-2.2.7/components/header' + dotcss,
        'src/css/default.css'
        ])
        .pipe(plumber())
        .pipe(concatcss('ihazspam.css'))
        .pipe(cleancss({ inline: 'all', keepSpecialComments: 0 }, function (error, minified) { }))
        .pipe(gulp.dest('wwwroot/pack/'));

    
    // Note: if using semantic.css packed, copy to wwwroot/pack/themes instead (relative font thing in css)
    //       required for icon (font-awesome)
    // console.log('>>> Copying static assets...')
    // gulp.src(['src/semantic-ui-2.2.7/themes/default/**']).pipe(gulp.dest('wwwroot/themes/default'));
});
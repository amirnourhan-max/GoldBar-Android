plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.amirnourhan.zarpulse"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.amirnourhan.zarpulse"
        minSdk = 26
        targetSdk = 35
        versionCode = 2
        versionName = "1.0.1"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    composeOptions {
        kotlinCompilerExtensionVersion = "1.5.15"
    }

    packaging {
        resources.excludes += "/META-INF/{AL2.0,LGPL2.1}"
    }
}

dependencies {
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.activity:activity-compose:1.10.0")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.7")

    implementation("androidx.compose.ui:ui:1.7.8")
    implementation("androidx.compose.ui:ui-tooling-preview:1.7.8")
    implementation("androidx.compose.foundation:foundation:1.7.8")
    implementation("androidx.compose.material3:material3:1.3.1")
    debugImplementation("androidx.compose.ui:ui-tooling:1.7.8")

    implementation("androidx.glance:glance-appwidget:1.2.0-rc01")
    implementation("androidx.glance:glance-material3:1.2.0-rc01")
    implementation("androidx.work:work-runtime-ktx:2.10.0")

    testImplementation("junit:junit:4.13.2")
}
